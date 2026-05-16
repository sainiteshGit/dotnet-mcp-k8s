// T034 — AKS cluster (Free control plane, Standard_B2s 1-2 autoscale)
// with OIDC issuer + Workload Identity enabled and Container Insights wired
// to the shared Log Analytics workspace.
//
// The cluster is only deployed when `enableAksCreation` is true. When an
// existing cluster is reused (per scripts/aks-discover.sh), main.bicep skips
// this module and reads the existing OIDC issuer URL from parameters.

@description('Whether to create the AKS cluster. False when reusing an existing cluster.')
param enableAksCreation bool

@description('Azure region. Pinned to eastus in main.bicep.')
param location string

@description('AKS cluster name.')
param clusterName string = 'aks-taskmgr'

@description('DNS prefix for the AKS API server.')
param dnsPrefix string = 'taskmgr'

@description('Kubernetes version. Defaults to AKS default for the region when empty.')
param kubernetesVersion string = ''

@description('Resource ID of the Log Analytics workspace for Container Insights.')
param logAnalyticsWorkspaceId string

@description('System node pool VM size. Locked to Standard_B2s for v1.')
param systemNodeVmSize string = 'Standard_B2s'

@description('Minimum node count for the cluster autoscaler.')
@minValue(1)
param systemNodeMinCount int = 1

@description('Maximum node count for the cluster autoscaler.')
@minValue(1)
param systemNodeMaxCount int = 2

resource aks 'Microsoft.ContainerService/managedClusters@2024-05-01' = if (enableAksCreation) {
  name: clusterName
  location: location
  sku: {
    name: 'Base'
    tier: 'Free'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    kubernetesVersion: empty(kubernetesVersion) ? null : kubernetesVersion
    dnsPrefix: dnsPrefix
    enableRBAC: true
    agentPoolProfiles: [
      {
        name: 'system'
        mode: 'System'
        osType: 'Linux'
        osSKU: 'AzureLinux'
        vmSize: systemNodeVmSize
        count: systemNodeMinCount
        enableAutoScaling: true
        minCount: systemNodeMinCount
        maxCount: systemNodeMaxCount
        type: 'VirtualMachineScaleSets'
      }
    ]
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
    }
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      workloadIdentity: {
        enabled: true
      }
    }
    addonProfiles: {
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalyticsWorkspaceId
        }
      }
    }
  }
}

output clusterName string = enableAksCreation ? aks.name : ''
output oidcIssuerUrl string = enableAksCreation ? aks.properties.oidcIssuerProfile.issuerURL : ''
output nodeResourceGroup string = enableAksCreation ? aks.properties.nodeResourceGroup : ''
output clusterResourceId string = enableAksCreation ? aks.id : ''
