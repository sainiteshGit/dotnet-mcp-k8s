// T031 — User-Assigned Managed Identity + Federated Identity Credential
// binding `system:serviceaccount:taskmgr:taskmgr-sa` to the AKS OIDC issuer.
// Used by both the Web App and MCP server pods via Azure Workload Identity.

@description('Azure region for the UAMI. Pinned to eastus in main.bicep.')
param location string

@description('Name of the User-Assigned Managed Identity.')
param uamiName string = 'uami-taskmgr'

@description('AKS cluster OIDC issuer URL (from infra/aks.discovered.json or the freshly-created AKS module output).')
param aksOidcIssuerUrl string

@description('Kubernetes namespace that hosts the federated workload.')
param namespace string = 'taskmgr'

@description('Kubernetes service account that hosts the federated workload.')
param serviceAccountName string = 'taskmgr-sa'

@description('Name of the federated identity credential resource.')
param federatedCredentialName string = 'fic-taskmgr-sa'

resource uami 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-07-31-preview' = {
  name: uamiName
  location: location
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-07-31-preview' = {
  parent: uami
  name: federatedCredentialName
  properties: {
    issuer: aksOidcIssuerUrl
    subject: 'system:serviceaccount:${namespace}:${serviceAccountName}'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output principalId string = uami.properties.principalId
output clientId string = uami.properties.clientId
output uamiResourceId string = uami.id
output uamiName string = uami.name
