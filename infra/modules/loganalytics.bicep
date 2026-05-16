// T033 — Log Analytics workspace (PerGB2018, 30-day retention) +
// workspace-based Application Insights component.

@description('Azure region. Pinned to eastus in main.bicep.')
param location string

@description('Log Analytics workspace name.')
param workspaceName string = 'log-taskmgr'

@description('Application Insights component name.')
param appInsightsName string = 'appi-taskmgr'

@description('Daily ingestion cap in GB. -1 means unlimited.')
param dailyQuotaGb int = -1

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output workspaceId string = workspace.id
output workspaceName string = workspace.name
output workspaceCustomerId string = workspace.properties.customerId
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
output appInsightsResourceId string = appInsights.id
