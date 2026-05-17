// T116a — Azure Monitor alerts required by the Constitution before prod
// cutover. Two rules are emitted:
//
//   1. Metric alert on Application Insights `requests/failed` filtered to
//      the `/readyz` probe — fires when any failed /readyz request is seen
//      over a 5-minute window (threshold > 0). This catches readiness
//      probe regressions before AKS pulls the Service offline.
//
//   2. Log-search (scheduledQueryRule v2) alert on the custom metric
//      `redaction_failures_total` — fires when ANY non-zero value lands
//      in the App Insights workspace over 5 minutes. Required by
//      Principle V (Secure-by-default): a redaction failure is a PII
//      leak risk and must page on first occurrence.
//
// Both rules attach to the EXISTING Action Group named
// `Application Insights Smart Detection` in this resource group — a
// reference, no new action group is created (per task T116a).

@description('Azure region. Most alert resources are global but scheduledQueryRules are regional.')
param location string

@description('Name of the Application Insights component to alert on. Re-bound below via `existing` so the scope id is typed.')
param appInsightsName string

@description('Name of the existing Action Group to attach both alerts to. Must already exist in this resource group.')
param actionGroupName string = 'Application Insights Smart Detection'

@description('Environment suffix (dev|prod) used in alert names.')
param environment string

// Reference (do NOT create) the action group. Bicep `existing` performs a
// read at deployment time and fails fast if the resource is absent.
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' existing = {
  name: actionGroupName
}

// Also re-bind the App Insights component so we can scope the metric alert
// at the typed resource id rather than passing a raw string (avoids the
// "Scope ... not of correct type" deployment error).
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// ------------------------------------------------------------------
// Alert 1 — /readyz 5xx metric alert.
// ------------------------------------------------------------------
resource readyzFailedAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-taskmgr-${environment}-readyz-failed'
  location: 'global'
  properties: {
    description: 'Fires when any failed /readyz request lands in Application Insights over 5 minutes. Catches readiness-probe regressions before AKS removes the pod from the Service.'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    targetResourceType: 'Microsoft.Insights/components'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ReadyzFailed'
          metricNamespace: 'microsoft.insights/components'
          metricName: 'requests/failed'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
          dimensions: [
            {
              name: 'request/name'
              operator: 'Include'
              values: [
                'GET /readyz'
                '/readyz'
              ]
            }
          ]
        }
      ]
    }
    autoMitigate: true
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// ------------------------------------------------------------------
// Alert 2 — redaction_failures_total log-search alert.
// ------------------------------------------------------------------
resource redactionFailuresAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-taskmgr-${environment}-redaction-failures'
  location: location
  kind: 'LogAlert'
  properties: {
    description: 'Fires when the redaction_failures_total custom metric is non-zero over a 5-minute window. A non-zero value indicates a PII redaction code path failed; Principle V mandates we page on first occurrence.'
    severity: 1
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "redaction_failures_total" and value > 0'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: false
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

output readyzAlertId string = readyzFailedAlert.id
output redactionAlertId string = redactionFailuresAlert.id
