@description('Location for all resources.')
param location string = resourceGroup().location

@minLength(5)
@maxLength(24)
@description('Name of the resources.')
param name string = 'paklo'

/* Managed Identity */
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: name
  location: location
#disable-next-line BCP073
  properties: { isolationScope: 'None' } // 'None' is the default, but this is required to avoid a bug in TTK
}

/* LogAnalytics */
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: name
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    workspaceCapping: {dailyQuotaGb: json('0.167') } // low so as not to pass the 5GB limit per subscription
    retentionInDays: 30
  }
}

/* AppConfiguration */
resource appConfiguration 'Microsoft.AppConfiguration/configurationStores@2024-06-01' = {
  name: name
  location: location
  properties: {
    dataPlaneProxy: { authenticationMode: 'Local', privateLinkDelegation: 'Disabled' }
    softDeleteRetentionInDays: 0 /* Free does not support this */
    defaultKeyValueRevisionRetentionPeriodInSeconds: 604800 /* 7 days */
  }
  sku: { name: 'free' }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {/*ttk bug*/ }
    }
  }
}

/* AppService Plan */
resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: name
  location: location
  kind: 'linux'
  properties: {
    reserved: true
    zoneRedundant: false // only for Premium tiers
  }
  sku: {
    name: 'F1'
  }
}

/* Container App Environment */
resource appEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
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
    peerAuthentication: { mtls: { enabled: false } }
    peerTrafficConfiguration: { encryption: { enabled: false } }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
  }
}

/* Application Insights */
resource appInsightsWebsite 'Microsoft.Insights/components@2020-02-02' = {
  name: '${name}-website'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logAnalyticsWorkspace.id }
}

/* Static WebApps */
resource staticWebAppWebsite 'Microsoft.Web/staticSites@2024-11-01' = {
  name: '${name}-website'
  location: location
  properties: {
    repositoryUrl: 'https://github.com/mburumaxwell/dependabot-azure-devops'
    branch: 'main'
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    provider: 'GitHub'
    enterpriseGradeCdnStatus: 'Disabled'
    #disable-next-line BCP037
    deploymentAuthPolicy: 'DeploymentToken'
    #disable-next-line BCP037
    #disable-next-line BCP037
    trafficSplitting: { environmentDistribution: { default: 100 } }
    publicNetworkAccess: 'Enabled'
  }
  sku: { name: 'Free', tier: 'Free' }
  // identity: { type: 'UserAssigned', userAssignedIdentities: { '${managedIdentity.id}': {} } }
}

/** WebApps */
resource websiteApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${name}-website'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    clientAffinityEnabled: false
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    autoGeneratedDomainNameLabelScope: 'ResourceGroupReuse'
    siteConfig: { linuxFxVersion: 'NODE|22-lts' }
  }

  resource scmCredentials 'basicPublishingCredentialsPolicies' = { name: 'scm', properties: { allow: false } }
  resource ftpCredentials 'basicPublishingCredentialsPolicies' = { name: 'ftp', properties: { allow: false } }

  resource siteConfig 'config' = {
    name: 'web'
    properties: { linuxFxVersion: 'NODE|22-lts', appCommandLine: 'node server.js' }
  }
}

/* Role Assignments */
resource appConfigurationDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentity.id, 'AppConfigurationDataReader')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
