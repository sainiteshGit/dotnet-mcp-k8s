#!/usr/bin/env bash
# aks-discover.sh
#
# Implements the AKS reuse-or-create discovery defined in research.md §5.
# Lists AKS clusters in resource group 'sainitesh-test'. If any cluster exists,
# selects the first one alphabetically and writes its name, OIDC issuer URL,
# and nodeResourceGroup to infra/aks.discovered.json.  If no cluster is found,
# writes a JSON document signalling that aks.bicep must be invoked to create one.
#
# Usage: scripts/aks-discover.sh [<resource-group>]
# Default RG: sainitesh-test
#
# The output JSON shape is consumed by infra/main.bicep at deploy time.

set -euo pipefail

readonly RG="${1:-sainitesh-test}"
readonly OUT_DIR="infra"
readonly OUT_FILE="${OUT_DIR}/aks.discovered.json"

if ! command -v az >/dev/null 2>&1; then
  echo "ERROR: 'az' (Azure CLI) is not installed or not on PATH." >&2
  exit 2
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: 'jq' is required." >&2
  exit 2
fi

mkdir -p "${OUT_DIR}"

echo "Discovering AKS clusters in resource group '${RG}'..."
CLUSTERS="$(az aks list -g "${RG}" -o json 2>/dev/null || echo '[]')"
COUNT="$(jq 'length' <<<"${CLUSTERS}")"

if [[ "${COUNT}" -eq 0 ]]; then
  echo "No existing AKS cluster found in '${RG}'. aks.bicep will create one."
  jq -n --arg rg "${RG}" '{
    exists: false,
    enableAksCreation: true,
    resourceGroup: $rg,
    name: null,
    oidcIssuerUrl: null,
    nodeResourceGroup: null
  }' > "${OUT_FILE}"
  echo "Wrote ${OUT_FILE}"
  exit 0
fi

# Reuse: pick the first cluster sorted by name (stable, deterministic).
SELECTED="$(jq '[.[]] | sort_by(.name) | .[0]' <<<"${CLUSTERS}")"
NAME="$(jq -r '.name' <<<"${SELECTED}")"
ISSUER="$(jq -r '.oidcIssuerProfile.issuerUrl // empty' <<<"${SELECTED}")"
NRG="$(jq -r '.nodeResourceGroup' <<<"${SELECTED}")"

if [[ -z "${ISSUER}" ]]; then
  echo "ERROR: cluster '${NAME}' has no OIDC issuer URL." >&2
  echo "Enable the OIDC issuer profile before reusing this cluster:" >&2
  echo "  az aks update -g ${RG} -n ${NAME} --enable-oidc-issuer --enable-workload-identity" >&2
  exit 1
fi

echo "Reusing existing AKS cluster:"
echo "  name:              ${NAME}"
echo "  oidcIssuerUrl:     ${ISSUER}"
echo "  nodeResourceGroup: ${NRG}"

jq -n \
  --arg rg "${RG}" \
  --arg name "${NAME}" \
  --arg issuer "${ISSUER}" \
  --arg nrg "${NRG}" \
  '{
    exists: true,
    enableAksCreation: false,
    resourceGroup: $rg,
    name: $name,
    oidcIssuerUrl: $issuer,
    nodeResourceGroup: $nrg
  }' > "${OUT_FILE}"

echo "Wrote ${OUT_FILE}"
