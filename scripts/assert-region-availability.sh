#!/usr/bin/env bash
# assert-region-availability.sh
#
# Fails fast if the target Azure region cannot host the v1 cost-optimized SKU profile.
# Re-verifies, before any Bicep deploy, that:
#   (a) the region is in the subscription's "Recommended" physical region list;
#   (b) Standard_B2s VM SKU is available with NO restrictions;
#   (c) PostgreSQL Flexible Server SKU Standard_B1ms is listed for the region;
#   (d) Microsoft.ContainerService/managedClusters is offered in the region.
#
# Usage:   scripts/assert-region-availability.sh [<region>]
# Default: eastus
#
# Pinned by research.md §11 and tasks.md (T006a, T012a). Wired as the SECOND job
# of CI (after assert-context, before any Bicep deploy).

set -euo pipefail

readonly REGION="${1:-eastus}"

if ! command -v az >/dev/null 2>&1; then
  echo "ERROR: 'az' (Azure CLI) is not installed or not on PATH." >&2
  exit 2
fi

fail=0

# ----- (a) Recommended region list -----
echo "Checking region '${REGION}' is in the subscription's Recommended physical region list..."
RECOMMENDED="$(az account list-locations \
  --query "[?metadata.regionCategory=='Recommended'].name" -o tsv 2>/dev/null || true)"

if [[ -z "${RECOMMENDED}" ]]; then
  echo "ERROR: failed to read recommended-region list. Are you logged in to Azure?" >&2
  exit 1
fi

if ! grep -Fxq "${REGION}" <<<"${RECOMMENDED}"; then
  echo "ERROR (a): region '${REGION}' is NOT in the subscription's Recommended list." >&2
  echo "Recommended regions:" >&2
  echo "${RECOMMENDED}" | sed 's/^/  - /' >&2
  fail=1
else
  echo "  OK (a): '${REGION}' is Recommended."
fi

# ----- (b) Standard_B2s VM SKU availability -----
echo "Checking VM SKU 'Standard_B2s' has no restrictions in '${REGION}'..."
B2S_JSON="$(az vm list-skus --location "${REGION}" --size Standard_B2s -o json 2>/dev/null || echo '[]')"
B2S_COUNT="$(jq 'length' <<<"${B2S_JSON}")"

if [[ "${B2S_COUNT}" -eq 0 ]]; then
  echo "ERROR (b): VM SKU 'Standard_B2s' is not offered in '${REGION}'." >&2
  fail=1
else
  # Any non-empty restrictions array on any matching entry is a fail.
  RESTRICTED="$(jq '[.[] | select((.restrictions // []) | length > 0)] | length' <<<"${B2S_JSON}")"
  if [[ "${RESTRICTED}" -gt 0 ]]; then
    echo "ERROR (b): VM SKU 'Standard_B2s' has restrictions in '${REGION}':" >&2
    jq '[.[] | {name, locations, restrictions}]' <<<"${B2S_JSON}" >&2
    fail=1
  else
    echo "  OK (b): 'Standard_B2s' available with no restrictions."
  fi
fi

# ----- (c) PostgreSQL Flexible Server Standard_B1ms -----
echo "Checking PostgreSQL Flexible Server SKU 'Standard_B1ms' in '${REGION}'..."
# Some CLI versions return JSON; some return TSV. We just grep for the SKU name.
PG_RAW="$(az postgres flexible-server list-skus --location "${REGION}" -o json 2>/dev/null || echo '[]')"
if grep -q "Standard_B1ms" <<<"${PG_RAW}"; then
  echo "  OK (c): 'Standard_B1ms' available."
else
  echo "ERROR (c): PostgreSQL Flexible Server SKU 'Standard_B1ms' is NOT listed for '${REGION}'." >&2
  fail=1
fi

# ----- (d) AKS managedClusters available in region -----
echo "Checking Microsoft.ContainerService/managedClusters is offered in '${REGION}'..."
AKS_LOCATIONS="$(az provider show -n Microsoft.ContainerService \
  --query "resourceTypes[?resourceType=='managedClusters'].locations[]" -o tsv 2>/dev/null || true)"

if [[ -z "${AKS_LOCATIONS}" ]]; then
  echo "ERROR (d): could not enumerate AKS regions (is Microsoft.ContainerService registered?)." >&2
  fail=1
else
  # AKS locations are reported as display names ("East US") AND/OR canonical ids ("eastus")
  # depending on CLI version. Normalize both sides for a case/space-insensitive compare.
  norm() { tr '[:upper:]' '[:lower:]' | tr -d ' '; }
  REGION_NORM="$(printf '%s' "${REGION}" | norm)"
  if printf '%s\n' "${AKS_LOCATIONS}" | norm | grep -Fxq "${REGION_NORM}"; then
    echo "  OK (d): AKS managedClusters offered in '${REGION}'."
  else
    echo "ERROR (d): AKS managedClusters NOT offered in '${REGION}'." >&2
    echo "Offered regions:" >&2
    printf '%s\n' "${AKS_LOCATIONS}" | sed 's/^/  - /' >&2
    fail=1
  fi
fi

if [[ "${fail}" -ne 0 ]]; then
  echo "FAILED: region '${REGION}' does not satisfy v1 SKU-availability requirements." >&2
  exit 1
fi

echo "OK: region '${REGION}' satisfies all v1 SKU-availability requirements."
