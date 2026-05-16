#!/usr/bin/env bash
# lint-prod-overlay.sh
#
# Constitution Principle II enforcement: the prod Kustomize overlay must NEVER
# enable MCP write tools. Exits non-zero if any file under the given overlay
# directory sets MCP_ALLOW_MUTATIONS=true (case-insensitive, any common YAML
# / env-file syntax).
#
# Usage:   scripts/lint-prod-overlay.sh [<overlay-dir>]
# Default: deploy/overlays/prod
#
# Wired into CI as the 'lint-prod-overlay' job; must succeed before deploy.

set -euo pipefail

readonly TARGET_DIR="${1:-deploy/overlays/prod}"

if [[ ! -d "${TARGET_DIR}" ]]; then
  echo "ERROR: target directory does not exist: ${TARGET_DIR}" >&2
  exit 2
fi

# Regex matches the env-var name MCP_ALLOW_MUTATIONS being assigned the
# literal value 'true' (with or without quotes) across:
#   - KEY=VALUE (.env, ConfigMap literals)
#   - "name: KEY" / "value: VALUE" YAML pairs (env: block)
#   - data.KEY: VALUE in ConfigMap.data
# Case-insensitive on both the key and 'true'.
readonly PATTERN_KV='(^|[^A-Za-z0-9_])MCP_ALLOW_MUTATIONS[[:space:]]*[:=][[:space:]]*"?[Tt][Rr][Uu][Ee]"?'
readonly PATTERN_YAML_NAME_VALUE='name:[[:space:]]*"?MCP_ALLOW_MUTATIONS"?'

# First: find any file naming the variable at all.
MATCHED_FILES="$(grep -RIli -E "MCP_ALLOW_MUTATIONS" "${TARGET_DIR}" 2>/dev/null || true)"

if [[ -z "${MATCHED_FILES}" ]]; then
  echo "OK: no MCP_ALLOW_MUTATIONS references in ${TARGET_DIR}"
  exit 0
fi

violations=0
while IFS= read -r f; do
  [[ -z "$f" ]] && continue

  # Case 1: a KEY=VALUE / KEY: VALUE assignment on a single line.
  if grep -E -q "${PATTERN_KV}" "$f"; then
    echo "VIOLATION: ${f} sets MCP_ALLOW_MUTATIONS=true (single-line form):" >&2
    grep -nE "${PATTERN_KV}" "$f" | sed 's/^/  /' >&2
    violations=$((violations + 1))
    continue
  fi

  # Case 2: K8s env list — `- name: MCP_ALLOW_MUTATIONS` on one line followed
  # within the next 3 lines by `value: "true"`.
  if grep -E -q "${PATTERN_YAML_NAME_VALUE}" "$f"; then
    if awk '
      BEGIN { found=0 }
      /name:[[:space:]]*"?MCP_ALLOW_MUTATIONS"?/ { armed=3; next }
      armed > 0 {
        if ($0 ~ /value:[[:space:]]*"?[Tt][Rr][Uu][Ee]"?/) { found=1; exit }
        armed--
      }
      END { exit (found ? 0 : 1) }
    ' "$f"; then
      echo "VIOLATION: ${f} sets MCP_ALLOW_MUTATIONS=true (k8s env: name/value form):" >&2
      grep -nE "${PATTERN_YAML_NAME_VALUE}|value:[[:space:]]*\"?[Tt][Rr][Uu][Ee]\"?" "$f" | sed 's/^/  /' >&2
      violations=$((violations + 1))
      continue
    fi
  fi

  echo "INFO: ${f} mentions MCP_ALLOW_MUTATIONS but does not set it to true (allowed)."
done <<<"${MATCHED_FILES}"

if [[ "${violations}" -gt 0 ]]; then
  echo "FAILED: ${violations} file(s) in ${TARGET_DIR} enable MCP_ALLOW_MUTATIONS=true." >&2
  echo "Prod overlay MUST leave mutations disabled (constitution Principle II)." >&2
  exit 1
fi

echo "OK: no MCP_ALLOW_MUTATIONS=true assignments in ${TARGET_DIR}"
