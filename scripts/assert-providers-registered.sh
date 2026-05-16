#!/usr/bin/env bash
# assert-providers-registered.sh
#
# Fails fast if any required Azure resource provider is not in 'Registered'
# state in the pinned subscription. Lists every unregistered provider in the
# failure message together with the exact `az provider register` command to fix
# it.
#
# Pinned by tasks.md T012b. Wired as the THIRD CI job after assert-region;
# blocks all infra / deploy jobs.

set -euo pipefail

readonly REQUIRED_PROVIDERS=(
  Microsoft.ContainerService
  Microsoft.ContainerRegistry
  Microsoft.DBforPostgreSQL
  Microsoft.OperationalInsights
  Microsoft.Insights
  Microsoft.AlertsManagement
  Microsoft.ManagedIdentity
  Microsoft.Network
  Microsoft.Compute
  Microsoft.Storage
  Microsoft.Authorization
  Microsoft.OperationsManagement
)

if ! command -v az >/dev/null 2>&1; then
  echo "ERROR: 'az' (Azure CLI) is not installed or not on PATH." >&2
  exit 2
fi

unregistered=()

for rp in "${REQUIRED_PROVIDERS[@]}"; do
  state="$(az provider show -n "${rp}" --query registrationState -o tsv 2>/dev/null || true)"
  if [[ "${state}" != "Registered" ]]; then
    unregistered+=("${rp}|${state:-Unknown}")
  fi
done

if [[ "${#unregistered[@]}" -eq 0 ]]; then
  echo "OK: all ${#REQUIRED_PROVIDERS[@]} required resource providers are Registered."
  exit 0
fi

{
  echo "ERROR: the following Azure resource providers are NOT in 'Registered' state:"
  for entry in "${unregistered[@]}"; do
    rp="${entry%%|*}"
    state="${entry##*|}"
    echo "  - ${rp} (current state: ${state})"
  done
  echo
  echo "Run these commands (with the pinned subscription active) to register them:"
  for entry in "${unregistered[@]}"; do
    rp="${entry%%|*}"
    echo "  az provider register -n ${rp}"
  done
} >&2

exit 1
