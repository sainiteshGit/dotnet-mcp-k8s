#!/usr/bin/env bash
# assert-azure-context.sh
#
# Fails fast if the active Azure CLI context is not pinned to the expected
# subscription / resource group. The expected values are supplied via env vars
# (set in the CI workflow from repository variables; for local runs export them
# in your shell or a .env file). Hard-coding them in the repo is prohibited by
# the constitution's "no secrets / no tenant-identifying values" policy.
#
# Required env vars:
#   AZURE_SUBSCRIPTION_ID  expected subscription GUID
#   AZURE_RESOURCE_GROUP   expected resource group name
#
# Usage:   scripts/assert-azure-context.sh <resource-group>
# Example: AZURE_SUBSCRIPTION_ID=<guid> AZURE_RESOURCE_GROUP=<name> \
#          scripts/assert-azure-context.sh <name>

set -euo pipefail

readonly EXPECTED_SUB="${AZURE_SUBSCRIPTION_ID:?AZURE_SUBSCRIPTION_ID env var is required}"
readonly EXPECTED_RG="${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP env var is required}"

if [[ $# -lt 1 ]]; then
  echo "ERROR: missing required argument: <resource-group>" >&2
  echo "Usage: $0 <resource-group>" >&2
  exit 2
fi

readonly TARGET_RG="$1"

if ! command -v az >/dev/null 2>&1; then
  echo "ERROR: 'az' (Azure CLI) is not installed or not on PATH." >&2
  exit 2
fi

ACTUAL_SUB="$(az account show --query id -o tsv 2>/dev/null || true)"
if [[ -z "${ACTUAL_SUB}" ]]; then
  echo "ERROR: no active Azure subscription. Run 'az login' first." >&2
  exit 1
fi

if [[ "${ACTUAL_SUB}" != "${EXPECTED_SUB}" ]]; then
  echo "ERROR: active subscription mismatch." >&2
  echo "  expected: ${EXPECTED_SUB}" >&2
  echo "  actual:   ${ACTUAL_SUB}" >&2
  echo "Fix: az account set --subscription ${EXPECTED_SUB}" >&2
  exit 1
fi

if [[ "${TARGET_RG}" != "${EXPECTED_RG}" ]]; then
  echo "ERROR: target resource group mismatch." >&2
  echo "  expected: ${EXPECTED_RG}" >&2
  echo "  actual:   ${TARGET_RG}" >&2
  exit 1
fi

echo "OK: Azure context locked to subscription=${EXPECTED_SUB} resource-group=${EXPECTED_RG}"
