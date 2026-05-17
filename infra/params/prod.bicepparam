// T036 — prod environment parameter file.
// Subscription / Resource Group are NOT hard-coded here; they are supplied
// at deploy time via $AZURE_SUBSCRIPTION_ID / $AZURE_RESOURCE_GROUP and
// verified by scripts/assert-azure-context.sh.
// Region: eastus (locked)

using '../main.bicep'

param location = 'eastus'
param environment = 'prod'

// ACR names must be globally unique, 5-50 alphanumeric chars.
param acrName = 'acrtaskmgrprod'

// Postgres Flexible Server names must be globally unique within Azure DNS.
param postgresServerName = 'pg-taskmgr-prod'
param postgresDatabaseName = 'taskmgr'

// Prod always deploys a fresh detection result; do not pin to an existing
// cluster here. scripts/aks-discover.sh runs in CI and overrides via
// `--parameters` if reuse is detected.
param existingAksName = ''
param existingAksOidcIssuerUrl = ''

param logAnalyticsDailyQuotaGb = -1
