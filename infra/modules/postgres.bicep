// T067 — Azure Database for PostgreSQL Flexible Server, Burstable B1ms.
//
// Cost-optimized v1 footprint, single AZ, 32 GiB storage, version 16.
// AUTH: Entra ID ONLY (Microsoft.DBforPostgreSQL/flexibleServers/administrators
// pointing at the Task Manager UAMI). Password auth is disabled — Workload
// Identity in AKS exchanges the federated token for an AAD access token
// (scope `https://ossrdbms-aad.database.windows.net/.default`) at runtime.
//
// `pgcrypto` is enabled at server level so the WebApp's
// HasPostgresExtension("pgcrypto") in TaskDbContext lights up.
//
// Firewall: AllowAzureServices is opened so AKS egress reaches the server.

@description('Azure region. Pinned to eastus by main.bicep.')
param location string

@description('Globally-unique PG Flexible Server name (3-63 lowercase alphanumeric/hyphen).')
param serverName string

@description('PostgreSQL major version.')
@allowed([
  '14'
  '15'
  '16'
])
param postgresVersion string = '16'

@description('Burstable B1ms is the cheapest v1 SKU and satisfies the cost guard.')
param skuName string = 'Standard_B1ms'

@description('Storage in GiB. 32 is the minimum allowed for Flexible Server.')
@minValue(32)
param storageSizeGB int = 32

@description('Database name created on the server.')
param databaseName string = 'taskmgr'

@description('Application client/object ID of the UAMI (Entra app ID, NOT the same as principalId).')
param uamiClientId string

@description('Display name of the UAMI used as Entra-ID admin principal name.')
param uamiName string

@description('Microsoft Entra tenant ID hosting the UAMI.')
param tenantId string = subscription().tenantId

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: serverName
  location: location
  sku: {
    name: skuName
    tier: 'Burstable'
  }
  properties: {
    version: postgresVersion
    storage: {
      storageSizeGB: storageSizeGB
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenantId
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// Bind the UAMI as the Entra-ID admin. The principalName must be the UAMI's
// display name so `SET ROLE` / login works with `POSTGRES_USER=<uamiName>`.
resource entraAdmin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2023-12-01-preview' = {
  parent: postgres
  name: uamiClientId
  properties: {
    principalName: uamiName
    principalType: 'ServicePrincipal'
    tenantId: tenantId
  }
}

resource pgcryptoExtension 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-12-01-preview' = {
  parent: postgres
  name: 'azure.extensions'
  properties: {
    value: 'PGCRYPTO'
    source: 'user-override'
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgres
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output fqdn string = postgres.properties.fullyQualifiedDomainName
output serverName string = postgres.name
output databaseName string = database.name
output resourceId string = postgres.id
