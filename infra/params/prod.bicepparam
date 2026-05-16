// T036 — prod environment parameter file.
// Subscription: d3c24b47-6f06-4152-8ade-6be38ba31c8c
// Resource Group: sainitesh-test
// Region: eastus (locked)

using '../main.bicep'

param location = 'eastus'
param environment = 'prod'

// ACR names must be globally unique, 5-50 alphanumeric chars.
param acrName = 'acrtaskmgrprod'

// Prod always deploys a fresh detection result; do not pin to an existing
// cluster here. scripts/aks-discover.sh runs in CI and overrides via
// `--parameters` if reuse is detected.
param existingAksName = ''
param existingAksOidcIssuerUrl = ''

param logAnalyticsDailyQuotaGb = -1
