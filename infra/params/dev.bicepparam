// T036 — dev environment parameter file.
// Subscription / Resource Group are NOT hard-coded here; they are supplied
// at deploy time via $AZURE_SUBSCRIPTION_ID / $AZURE_RESOURCE_GROUP and
// verified by scripts/assert-azure-context.sh.
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
