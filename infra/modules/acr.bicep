// T032 — Azure Container Registry (Basic SKU) + AcrPull role assignment
// granting the Task Manager UAMI permission to pull images.

@description('Azure region for the ACR. Pinned to eastus in main.bicep.')
param location string

@description('Globally-unique ACR name. Must be 5-50 alphanumeric chars.')
param acrName string

@description('Principal ID (objectId) of the UAMI that needs AcrPull.')
param uamiPrincipalId string

// AcrPull built-in role definition ID (constant across all subscriptions).
var acrPullRoleDefinitionId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    anonymousPullEnabled: false
    zoneRedundancy: 'Disabled'
  }
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, uamiPrincipalId, acrPullRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleDefinitionId)
    principalId: uamiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output loginServer string = acr.properties.loginServer
output acrName string = acr.name
output acrResourceId string = acr.id
