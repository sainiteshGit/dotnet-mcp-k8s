// T036 — dev environment parameter file.
// Subscription: d3c24b47-6f06-4152-8ade-6be38ba31c8c
// Resource Group: sainitesh-test
// Region: eastus (locked)

using '../main.bicep'

param location = 'eastus'
param environment = 'dev'

// ACR names must be globally unique, 5-50 alphanumeric chars.
param acrName = 'acrtaskmgrdev'

// Postgres Flexible Server names must be globally unique within Azure DNS.
param postgresServerName = 'pg-taskmgr-dev'
param postgresDatabaseName = 'taskmgr'

// Reuse-or-create: populated by scripts/aks-discover.sh before deploy.
// Leave both empty to let aks.bicep create a fresh cluster.
param existingAksName = ''
param existingAksOidcIssuerUrl = ''

param logAnalyticsDailyQuotaGb = -1
