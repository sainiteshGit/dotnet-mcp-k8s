// T035 — main.bicep wires the cost-optimized v1 foundation:
//   loganalytics → acr → (optional aks) → uami (federated to AKS OIDC issuer).
//
// Subscription is pinned to d3c24b47-6f06-4152-8ade-6be38ba31c8c by the
// CI guard `scripts/assert-azure-context.sh sainitesh-test`. Region is
// re-verified by `scripts/assert-region-availability.sh eastus`.
//
// Reuse-or-create: if `existingAksName` is non-empty, the `aks` module is
// skipped and the OIDC issuer URL is read from `existingAksOidcIssuerUrl`
// (both values are produced by `scripts/aks-discover.sh` writing
// `infra/aks.discovered.json` and injected via the bicepparam file).

targetScope = 'resourceGroup'

@description('Azure region. Pinned to eastus.')
@allowed([
  'eastus'
])
param location string = 'eastus'

@description('Deployment environment (dev or prod).')
@allowed([
  'dev'
  'prod'
])
param environment string

@description('Globally-unique ACR name (5-50 alphanumeric chars).')
param acrName string

@description('Name of an existing AKS cluster to reuse. Empty string => create a new one via aks.bicep.')
param existingAksName string = ''

@description('OIDC issuer URL of the existing AKS cluster. Required when existingAksName is non-empty.')
param existingAksOidcIssuerUrl string = ''

@description('Daily ingestion cap in GB for Log Analytics. -1 means unlimited.')
param logAnalyticsDailyQuotaGb int = -1

@description('Globally-unique Postgres Flexible Server name (3-63 lowercase alphanumeric/hyphen).')
param postgresServerName string

@description('Postgres database name created on the server. Default `taskmgr`.')
param postgresDatabaseName string = 'taskmgr'

var createAks = empty(existingAksName)

module loganalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics-${environment}'
  params: {
    location: location
    workspaceName: 'log-taskmgr-${environment}'
    appInsightsName: 'appi-taskmgr-${environment}'
    dailyQuotaGb: logAnalyticsDailyQuotaGb
  }
}

module aks 'modules/aks.bicep' = {
  name: 'aks-${environment}'
  params: {
    enableAksCreation: createAks
    location: location
    clusterName: 'aks-taskmgr-${environment}'
    dnsPrefix: 'taskmgr-${environment}'
    logAnalyticsWorkspaceId: loganalytics.outputs.workspaceId
  }
}

var resolvedOidcIssuerUrl = createAks ? aks.outputs.oidcIssuerUrl : existingAksOidcIssuerUrl
var resolvedAksName = createAks ? aks.outputs.clusterName : existingAksName

module uami 'modules/uami.bicep' = {
  name: 'uami-${environment}'
  params: {
    location: location
    uamiName: 'uami-taskmgr-${environment}'
    aksOidcIssuerUrl: resolvedOidcIssuerUrl
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr-${environment}'
  params: {
    location: location
    acrName: acrName
    uamiPrincipalId: uami.outputs.principalId
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres-${environment}'
  params: {
    location: location
    serverName: postgresServerName
    databaseName: postgresDatabaseName
    uamiClientId: uami.outputs.clientId
    uamiName: uami.outputs.uamiName
  }
}

// Outputs (re-exported from every module so deploy scripts can capture them).
output logAnalyticsWorkspaceId string = loganalytics.outputs.workspaceId
output logAnalyticsWorkspaceName string = loganalytics.outputs.workspaceName
output appInsightsConnectionString string = loganalytics.outputs.appInsightsConnectionString
output appInsightsResourceId string = loganalytics.outputs.appInsightsResourceId

output acrLoginServer string = acr.outputs.loginServer
output acrName string = acr.outputs.acrName
output acrResourceId string = acr.outputs.acrResourceId

output aksClusterName string = resolvedAksName
output aksOidcIssuerUrl string = resolvedOidcIssuerUrl
output aksNodeResourceGroup string = createAks ? aks.outputs.nodeResourceGroup : ''
output aksCreated bool = createAks

output uamiPrincipalId string = uami.outputs.principalId
output uamiClientId string = uami.outputs.clientId
output uamiResourceId string = uami.outputs.uamiResourceId
output uamiName string = uami.outputs.uamiName

output postgresFqdn string = postgres.outputs.fqdn
output postgresServerName string = postgres.outputs.serverName
output postgresDatabaseName string = postgres.outputs.databaseName
output postgresResourceId string = postgres.outputs.resourceId
