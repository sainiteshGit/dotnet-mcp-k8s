#!/usr/bin/env bash
# T107 — Test harness for scripts/lint-prod-overlay.sh.
#
# Validates two contracts:
#   1) Running the script against a synthetic directory that contains a
#      manifest enabling MCP_ALLOW_MUTATIONS=true MUST exit non-zero.
#   2) Running the script against the real deploy/overlays/prod/ MUST exit zero
#      (the prod overlay never enables mutations).
#
# Invoked from CI via `bash tests/Scripts/LintProdOverlayTests.sh`. Designed
# to be runnable locally without any toolchain beyond bash and grep.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
LINTER="${REPO_ROOT}/scripts/lint-prod-overlay.sh"

if [[ ! -x "${LINTER}" ]]; then
  echo "FAIL: linter script not executable at ${LINTER}" >&2
  exit 2
fi

pass=0
fail=0

assert_exits_nonzero() {
  local label="$1"; shift
  if "$@" >/tmp/lint-out.$$ 2>&1; then
    echo "FAIL: ${label} — expected non-zero exit, got 0"
    sed 's/^/    /' /tmp/lint-out.$$
    fail=$((fail + 1))
  else
    echo "PASS: ${label}"
    pass=$((pass + 1))
  fi
  rm -f /tmp/lint-out.$$
}

assert_exits_zero() {
  local label="$1"; shift
  if "$@" >/tmp/lint-out.$$ 2>&1; then
    echo "PASS: ${label}"
    pass=$((pass + 1))
  else
    echo "FAIL: ${label} — expected zero exit, got non-zero"
    sed 's/^/    /' /tmp/lint-out.$$
    fail=$((fail + 1))
  fi
  rm -f /tmp/lint-out.$$
}

# ---------------------------------------------------------------------------
# Case 1: synthetic prod overlay that DOES enable mutations -> must FAIL.
# ---------------------------------------------------------------------------
tmpdir="$(mktemp -d -t lint-prod-overlay-test.XXXXXX)"
trap 'rm -rf "${tmpdir}"' EXIT

mkdir -p "${tmpdir}/violating-overlay"
cat >"${tmpdir}/violating-overlay/mcp-patch.yaml" <<'YAML'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mcp-server
spec:
  template:
    spec:
      containers:
        - name: mcp-server
          env:
            - name: MCP_ALLOW_MUTATIONS
              value: "true"
YAML
assert_exits_nonzero "violating overlay rejected (yaml env: name/value form)" \
  "${LINTER}" "${tmpdir}/violating-overlay"

# Same content via a flat KEY=VALUE file (simulates ConfigMap literal or .env).
mkdir -p "${tmpdir}/violating-envfile"
cat >"${tmpdir}/violating-envfile/.env.prod" <<'ENV'
MCP_ALLOW_MUTATIONS=true
ENV
assert_exits_nonzero "violating overlay rejected (KEY=VALUE form)" \
  "${LINTER}" "${tmpdir}/violating-envfile"

# ---------------------------------------------------------------------------
# Case 2: the real deploy/overlays/prod/ MUST pass.
# ---------------------------------------------------------------------------
PROD_OVERLAY="${REPO_ROOT}/deploy/overlays/prod"
if [[ ! -d "${PROD_OVERLAY}" ]]; then
  echo "FAIL: deploy/overlays/prod missing — cannot run real-overlay check"
  fail=$((fail + 1))
else
  assert_exits_zero "real prod overlay accepted" "${LINTER}" "${PROD_OVERLAY}"
fi

echo
echo "===================="
echo "passed: ${pass}"
echo "failed: ${fail}"

if [[ "${fail}" -gt 0 ]]; then
  exit 1
fi
exit 0
