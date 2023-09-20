@description('Location for all resources.')
param location string = resourceGroup().location

@description('Name of the resources')
param name string = 'dependabot'

@description('Whether to synchronize repositories on startup.')
param synchronizeOnStartup bool = false

@description('Whether to create or update subscriptions on startup.')
param createOrUpdateWebhooksOnStartup bool = false

@description('JSON array string fo projects to setup. E.g. [{"url":"https://dev.azure.com/tingle/dependabot","token":"dummy","AutoComplete":true}]')
param projectSetups string = '[]'

@description('Access token for authenticating requests to GitHub.')
param githubToken string = ''

@description('Tag of the docker images.')
param imageTag string = '#{GITVERSION_NUGETVERSIONV2}#'

@minValue(1)
@maxValue(2)
@description('The minimum number of replicas')
param minReplicas int = 1 // necessary for in-memory scheduling

@minValue(1)
@maxValue(5)
@description('The maximum number of replicas')
param maxReplicas int = 1

var fileShares = [
  { name: 'certs' }
  { name: 'distributed-locks', writeable: true }
  { name: 'working-dir', writeable: true }
]

var sqlServerAdministratorLogin = uniqueString(resourceGroup().id) // e.g. zecnx476et7xm (13 characters)
var sqlServerAdministratorLoginPassword = '${skip(uniqueString(resourceGroup().id), 5)}%${uniqueString('sql-password', resourceGroup().id)}' // e.g. abcde%zecnx476et7xm (19 characters)
// avoid conflicts across multiple deployments for resources that generate FQDN based on the name
var collisionSuffix = uniqueString(resourceGroup().id) // e.g. zecnx476et7xm (13 characters)
var queueNames = [
  'process-synchronization'
  'repository-created'
  'repository-updated'
  'repository-deleted'
  'trigger-update-jobs'
  'update-job-check-state'
  'update-job-collect-logs'
]
var queueScaleRules = [for qn in queueNames: {
  name: 'azure-servicebus-${qn}'
  custom: {
    type: 'azure-servicebus'
    metadata: {
      namespace: serviceBusNamespace.name // Name of the Azure Service Bus namespace that contains your queue or topic.
      queueName: qn // Name of the Azure Service Bus queue to scale on.
      messageCount: '100' // Amount of active messages in your Azure Service Bus queue or topic to scale on.
    }
    auth: [ { secretRef: 'connection-strings-asb-scaler', triggerParameter: 'connection' } ]
  }
}]

/* Managed Identity */
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
}

/* Service Bus namespace */
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: '${name}-${collisionSuffix}'
  location: location
  properties: { disableLocalAuth: false, zoneRedundant: false }
  sku: { name: 'Basic' }

  resource authorizationRule 'AuthorizationRules' existing = { name: 'RootManageSharedAccessKey' }
}

/* AppConfiguration */
resource appConfiguration 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: '${name}-${collisionSuffix}'
  location: location
  properties: { softDeleteRetentionInDays: 0 /* Free does not support this */ }
  sku: { name: 'free' }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {/*ttk bug*/ }
    }
  }
}

/* Storage Account */
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${name}${collisionSuffix}' // hyphens not allowed
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }

  resource fileServices 'fileServices' existing = {
    name: 'default'

    resource shares 'shares' = [for fs in fileShares: {
      name: fs.name
      properties: {
        accessTier: contains(fs, 'accessTier') ? fs.accessTier : 'TransactionOptimized'
        // container apps does not support NFS
        // https://github.com/microsoft/azure-container-apps/issues/717
        // enabledProtocols: contains(fs, 'enabledProtocols') ? fs.enabledProtocols : 'SMB'
        shareQuota: contains(fs, 'shareQuota') ? fs.shareQuota : 1
      }
    }]
  }
}

/* SQL Server */
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: '${name}-${collisionSuffix}'
  location: location
  properties: {
    publicNetworkAccess: 'Enabled'
    administratorLogin: sqlServerAdministratorLogin
    administratorLoginPassword: sqlServerAdministratorLoginPassword
    primaryUserAssignedIdentityId: managedIdentity.id
    restrictOutboundNetworkAccess: 'Disabled'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {/*ttk bug*/ }
    }
  }
}
resource sqlServerFirewallRuleForAzure 'Microsoft.Sql/servers/firewallRules@2022-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: { endIpAddress: '0.0.0.0', startIpAddress: '0.0.0.0' }
}
resource sqlServerDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: name
  location: location
  sku: { name: 'Basic' }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Geo'
    isLedgerOn: false
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {/*ttk bug*/ }
    }
  }
}

/* LogAnalytics */
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    workspaceCapping: {
      dailyQuotaGb: json('0.167') // low so as not to pass the 5GB limit per subscription
    }
  }
}

/* Container App Environment */
resource appEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: name
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }

  resource storages 'storages' = [for fs in fileShares: {
    name: fs.name
    properties: {
      azureFile: {
        accountName: storageAccount.name
        accountKey: storageAccount.listKeys().keys[0].value
        shareName: fs.name
        accessMode: contains(fs, 'writeable') && bool(fs.writeable) ? 'ReadWrite' : 'ReadOnly'
      }
    }
  }]
}

/* Application Insights */
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

/* Container App */
resource app 'Microsoft.App/containerApps@2023-05-01' = {
  name: name
  location: location
  properties: {
    managedEnvironmentId: appEnvironment.id
    configuration: {
      ingress: { external: true, targetPort: 80, traffic: [ { latestRevision: true, weight: 100 } ] }
      secrets: [
        { name: 'connection-strings-application-insights', value: appInsights.properties.ConnectionString }
        { name: 'project-setups', value: projectSetups }
        {
          name: 'connection-strings-sql'
          value: join([
              'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433'
              'Initial Catalog=${sqlServerDatabase.name}'
              'User ID=${sqlServerAdministratorLogin}'
              'Password=${sqlServerAdministratorLoginPassword}'
              'Persist Security Info=False'
              'MultipleActiveResultSets=False'
              'Encrypt=True'
              'TrustServerCertificate=False'
              'Connection Timeout=30'
            ], ';')
        }
        { name: 'connection-strings-asb-scaler', value: serviceBusNamespace::authorizationRule.listKeys().primaryConnectionString }
      ]
    }
    template: {
      containers: [
        {
          image: 'ghcr.io/tinglesoftware/dependabot-server:${imageTag}'
          name: 'dependabot'
          volumeMounts: [
            { mountPath: '/mnt/dependabot', volumeName: 'working-dir' }
            { mountPath: '/mnt/distributed-locks', volumeName: 'distributed-locks' }
          ]
          env: [
            { name: 'AZURE_CLIENT_ID', value: managedIdentity.properties.clientId } // Specifies the User-Assigned Managed Identity to use. Without this, the app attempt to use the system assigned one.
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' } // Application is behind proxy
            { name: 'EFCORE_PERFORM_MIGRATIONS', value: 'true' } // Perform migrations on startup

            { name: 'PROJECT_SETUPS', secretRef: 'project-setups' }

            { name: 'AzureAppConfig__Endpoint', value: appConfiguration.properties.endpoint }
            { name: 'AzureAppConfig__Label', value: 'Production' }

            { name: 'ApplicationInsights__ConnectionString', secretRef: 'connection-strings-application-insights' }
            { name: 'ConnectionStrings__Sql', secretRef: 'connection-strings-sql' }

            { name: 'DistributedLocking__FilePath', value: '/mnt/distributed-locks' }

            { name: 'Logging__ApplicationInsights__LogLevel__Default', value: 'None' } // do not send logs to application insights (duplicates LogAnalytics)
            { name: 'Logging__Seq__LogLevel__Default', value: 'Warning' }
            { name: 'Logging__Seq__ServerUrl', value: '' } // set via AppConfig
            { name: 'Logging__Seq__ApiKey', value: '' } // set via AppConfig

            { name: 'Workflow__SynchronizeOnStartup', value: synchronizeOnStartup ? 'true' : 'false' }
            { name: 'Workflow__CreateOrUpdateWebhooksOnStartup', value: createOrUpdateWebhooksOnStartup ? 'true' : 'false' }
            { name: 'Workflow__JobsApiUrl', value: 'https://${name}.${appEnvironment.properties.defaultDomain}' }
            { name: 'Workflow__WorkingDirectory', value: '/mnt/dependabot' }
            { name: 'Workflow__WebhookEndpoint', value: 'https://${name}.${appEnvironment.properties.defaultDomain}/webhooks/azure' }
            { name: 'Workflow__ResourceGroupId', value: resourceGroup().id }
            { name: 'Workflow__AppEnvironmentId', value: appEnvironment.id }
            { name: 'Workflow__LogAnalyticsWorkspaceId', value: logAnalyticsWorkspace.properties.customerId }
            { name: 'Workflow__UpdaterContainerImageTemplate', value: 'ghcr.io/tinglesoftware/dependabot-updater-{{ecosystem}}:${imageTag}' }
            { name: 'Workflow__GithubToken', value: githubToken }
            { name: 'Workflow__Location', value: location }

            { name: 'Authentication__Schemes__Management__Authority', value: '${environment().authentication.loginEndpoint}${subscription().tenantId}/v2.0' }
            { name: 'Authentication__Schemes__Management__ValidAudiences__0', value: 'https://${name}.${appEnvironment.properties.defaultDomain}' }

            { name: 'EventBus__SelectedTransport', value: 'ServiceBus' }
            { name: 'EventBus__Transports__azure-service-bus__FullyQualifiedNamespace', value: split(split(serviceBusNamespace.properties.serviceBusEndpoint, '/')[2], ':')[0] } // manipulating https://{your-namespace}.servicebus.windows.net:443/
          ]
          resources: {// these are the least resources we can provision
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          probes: [
            { type: 'Liveness', httpGet: { port: 80, path: '/liveness' } }
            {
              type: 'Readiness'
              httpGet: { port: 80, path: '/health' }
              failureThreshold: 10
              initialDelaySeconds: 3
              timeoutSeconds: 5
            }
          ]
        }
      ]
      volumes: [
        { name: 'working-dir', storageName: 'working-dir', storageType: 'AzureFile' }
        { name: 'distributed-locks', storageName: 'distributed-locks', storageType: 'AzureFile' }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: concat(
          [ { name: 'http', http: { metadata: { concurrentRequests: '1000' } } } ],
          queueScaleRules)
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {/*ttk bug*/ }
    }
  }
}

/* Role Assignments */
resource contributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {// needed for creating jobs
  name: guid(managedIdentity.id, 'ContributorRoleAssignment')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource serviceBusDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentity.id, 'AzureServiceBusDataOwner')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource appConfigurationDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentity.id, 'AppConfigurationDataReader')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentity.id, 'StorageBlobDataContributor')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource logAnalyticsReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentity.id, 'LogAnalyticsReader')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '73c42c96-874c-492b-b04d-ab87d138a893')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

#disable-next-line outputs-should-not-contain-secrets
output sqlServerAdministratorLoginPassword string = sqlServerAdministratorLoginPassword
output webhookEndpoint string = 'https://${app.properties.configuration.ingress.fqdn}/webhooks/azure'
