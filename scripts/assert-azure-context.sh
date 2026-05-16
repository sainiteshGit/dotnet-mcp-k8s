#!/usr/bin/env bash
# assert-azure-context.sh
#
# Fails fast if the active Azure CLI context is not pinned to:
#   subscription: d3c24b47-6f06-4152-8ade-6be38ba31c8c
#   resource group (passed as $1): sainitesh-test
#
# Usage:   scripts/assert-azure-context.sh <resource-group>
# Example: scripts/assert-azure-context.sh sainitesh-test
#
# Pinned by plan.md / constitution.md. Wired as the FIRST job of CI;
# every downstream job depends on a green run of this script.

set -euo pipefail

readonly EXPECTED_SUB="d3c24b47-6f06-4152-8ade-6be38ba31c8c"
readonly EXPECTED_RG="sainitesh-test"

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
