@description('Location for all resources.')
param location string = resourceGroup().location

@description('Name of the resources')
param name string = 'dependabot'

@description('URL of the project. For example "https://dev.azure.com/fabrikam/DefaultCollection"')
param projectUrl string

@description('Token for accessing the project.')
param projectToken string

@description('Whether to synchronize repositories on startup.')
param synchronizeOnStartup bool = false

@description('Whether to create or update subscriptions on startup.')
param createOrUpdateWebhooksOnStartup bool = false

@description('Access token for authenticating requests to GitHub.')
param githubToken string = ''

@description('Whether to set auto complete on created pull requests.')
param autoComplete bool = true

@description('Identifiers of configs to be ignored in auto complete. E.g 3,4,10')
param autoCompleteIgnoreConfigs array = []

@allowed([
  'NoFastForward'
  'Rebase'
  'RebaseMerge'
  'Squash'
])
@description('Merge strategy to use when setting auto complete on created pull requests.')
param autoCompleteMergeStrategy string = 'Squash'

@description('Whether to automatically approve created pull requests.')
param autoApprove bool = false

@allowed([
  'ContainerInstances'
  // 'ContainerApps' // TODO: resotre this once jobs support is added
])
@description('Where to host new update jobs.')
param jobHostType string = 'ContainerInstances'

@description('Password for Webhooks, ServiceHooks, and Notifications from Azure DevOps.')
#disable-next-line secure-secrets-in-params // need sensible defaults
param notificationsPassword string = uniqueString('service-hooks', resourceGroup().id) // e.g. zecnx476et7xm (13 characters)

@description('Registry of the docker image. E.g. "contoso.azurecr.io". Leave empty unless you have a private registry mirroring the image from docker hub')
param dockerImageRegistry string = 'ghcr.io'

@description('Registry and repository of the server docker image. Ideally, you do not need to edit this value.')
param serverImageRepository string = 'tinglesoftware/dependabot-server'

@description('Tag of the server docker image.')
param serverImageTag string = '#{GITVERSION_NUGETVERSIONV2}#'

@description('Registry and repository of the updater docker image. Ideally, you do not need to edit this value.')
param updaterImageRepository string = 'tinglesoftware/dependabot-updater'

@description('Tag of the updater docker image.')
param updaterImageTag string = '#{GITVERSION_NUGETVERSIONV2}#'

@minValue(1)
@maxValue(2)
@description('The minimum number of replicas')
param minReplicas int = 1 // necessary for in-memory scheduling

@minValue(1)
@maxValue(5)
@description('The maximum number of replicas')
param maxReplicas int = 1

var sqlServerAdministratorLogin = uniqueString(resourceGroup().id) // e.g. zecnx476et7xm (13 characters)
var sqlServerAdministratorLoginPassword = '${skip(uniqueString(resourceGroup().id), 5)}%${uniqueString('sql-password', resourceGroup().id)}' // e.g. abcde%zecnx476et7xm (19 characters)
var hasDockerImageRegistry = (dockerImageRegistry != null && !empty(dockerImageRegistry))
var isAcrServer = hasDockerImageRegistry && endsWith(dockerImageRegistry, environment().suffixes.acrLoginServer)
// avoid conflicts across multiple deployments for resources that generate FQDN based on the name
var collisionSuffix = uniqueString(resourceGroup().id) // e.g. zecnx476et7xm (13 characters)

/* Managed Identities */
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: name
  location: location
}
resource managedIdentityJobs 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: '${name}-jobs'
  location: location
}

/* Service Bus namespace */
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: '${name}-${collisionSuffix}'
  location: location
  properties: {
    disableLocalAuth: false
    zoneRedundant: false
  }
  sku: {
    name: 'Basic'
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
      '${managedIdentity.id}': { /*ttk bug*/}
    }
  }
}
resource sqlServerDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: name
  location: location
  sku: {
    name: 'Basic'
  }
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
      '${managedIdentity.id}': { /*ttk bug*/}
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
resource appEnvironment 'Microsoft.App/managedEnvironments@2022-06-01-preview' = {
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
    zoneRedundant: false // enabling this requires a custom VNET with an intrastrucutre Subnet
  }
  sku: {
    name: 'Consumption'
  }
}

/* Application Insights */
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    // WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

/* Container App */
resource app 'Microsoft.App/containerApps@2022-06-01-preview' = {
  name: name
  location: location
  properties: {
    managedEnvironmentId: appEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: isAcrServer ? [
        {
          identity: managedIdentity.id
          server: dockerImageRegistry
        }
      ] : []
      secrets: [
        { name: 'connection-strings-application-insights', value: appInsights.properties.ConnectionString }
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
        { name: 'notifications-password', value: notificationsPassword }
        { name: 'project-token', value: projectToken }
        { name: 'log-analytics-workspace-key', value: logAnalyticsWorkspace.listKeys().primarySharedKey }
      ]
    }
    template: {
      containers: [
        {
          image: '${'${hasDockerImageRegistry ? '${dockerImageRegistry}/' : ''}'}${serverImageRepository}:${serverImageTag}'
          name: 'dependabot'
          env: [
            { name: 'AZURE_CLIENT_ID', value: managedIdentity.properties.clientId } // Specifies the User-Assigned Managed Identity to use. Without this, the app attempt to use the system assigned one.
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' } // Application is behind proxy
            { name: 'EFCORE_PERFORM_MIGRATIONS', value: 'true' } // Perform migrations on startup

            { name: 'ApplicationInsights__ConnectionString', secretRef: 'connection-strings-application-insights' }
            { name: 'ConnectionStrings__Sql', secretRef: 'connection-strings-sql' }

            { name: 'Workflow__SynchronizeOnStartup', value: synchronizeOnStartup ? 'true' : 'false' }
            { name: 'Workflow__LoadSchedulesOnStartup', value: 'true' }
            { name: 'Workflow__CreateOrUpdateWebhooksOnStartup', value: createOrUpdateWebhooksOnStartup ? 'true' : 'false' }
            { name: 'Workflow__ProjectUrl', value: projectUrl }
            { name: 'Workflow__ProjectToken', secretRef: 'project-token' }
            { name: 'Workflow__WebhookEndpoint', value: 'https://${name}.${appEnvironment.properties.defaultDomain}/webhooks/azure' }
            { name: 'Workflow__ResourceGroupId', value: resourceGroup().id }
            { name: 'Workflow__LogAnalyticsWorkspaceId', value: logAnalyticsWorkspace.properties.customerId }
            { name: 'Workflow__LogAnalyticsWorkspaceKey', secretRef: 'log-analytics-workspace-key' }
            { name: 'Workflow__ManagedIdentityId', value: managedIdentityJobs.id }
            { name: 'Workflow__UpdaterContainerImage', value: '${'${hasDockerImageRegistry ? '${dockerImageRegistry}/' : ''}'}${updaterImageRepository}:${updaterImageTag}' }
            { name: 'Workflow__AutoComplete', value: autoComplete ? 'true' : 'false' }
            { name: 'Workflow__AutoCompleteIgnoreConfigs', value: join(autoCompleteIgnoreConfigs, ';') }
            { name: 'Workflow__AutoCompleteMergeStrategy', value: autoCompleteMergeStrategy }
            { name: 'Workflow__AutoApprove', value: autoApprove ? 'true' : 'false' }
            { name: 'Workflow__GithubToken', value: githubToken }
            { name: 'Workflow__JobHostType', value: jobHostType }
            { name: 'Workflow__Location', value: location }

            {
              name: 'Authentication__Schemes__Management__Authority'
              // Format: https://login.microsoftonline.com/{tenant-id}/v2.0
              value: '${environment().authentication.loginEndpoint}${subscription().tenantId}/v2.0'
            }
            { name: 'Authentication__Schemes__Management__ValidAudiences__0', value: 'https://${name}.${appEnvironment.properties.defaultDomain}' }
            { name: 'Authentication__Schemes__ServiceHooks__Credentials__vsts', secretRef: 'notifications-password' }

            { name: 'EventBus__SelectedTransport', value: 'ServiceBus' }
            {
              name: 'EventBus__Transports__azure-service-bus__FullyQualifiedNamespace'
              value: split(split(serviceBusNamespace.properties.serviceBusEndpoint, '/')[2], ':')[0] // manipulating https://{your-namespace}.servicebus.windows.net:443/
            }
          ]
          resources: { // these are the least resources we can provision
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': { /*ttk bug*/}
    }
  }
}

/* Role Assignments */
resource contributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { // needed for creating jobs
  name: guid(resourceGroup().id, 'managedIdentity', 'ContributorRoleAssignment')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource serviceBusDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'managedIdentity', 'AzureServiceBusDataOwner')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource logAnalyticsReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'managedIdentity', 'LogAnalyticsReader')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '73c42c96-874c-492b-b04d-ab87d138a893')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// output id string = app.id
// output fqdn string = app.properties.configuration.ingress.fqdn
#disable-next-line outputs-should-not-contain-secrets
output sqlServerAdministratorLoginPassword string = sqlServerAdministratorLoginPassword
output webhookEndpoint string = 'https://${name}.${appEnvironment.properties.defaultDomain}/webhooks/azure'
#disable-next-line outputs-should-not-contain-secrets
output notificationsPassword string = notificationsPassword
