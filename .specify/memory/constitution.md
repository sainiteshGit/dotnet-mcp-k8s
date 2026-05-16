<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0
Modified principles:
  - II. Read-Only by Default — retargeted from "K8s cluster state" to
    "backing API surface" to reflect new two-service architecture
    (web app + MCP wrapper). Wording broadened; semantics preserved
    (default-deny on mutations).
Added sections:
  - Service Topology (under Deployment & Operations)
Removed sections: None
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no edit needed
  - .specify/templates/spec-template.md ✅ no edit needed
  - .specify/templates/tasks-template.md ✅ no edit needed
  - .specify/templates/checklist-template.md ✅ no edit needed
  - .github/prompts/speckit.*.prompt.md ✅ no edit needed
  - README.md ⚠ still pending (create at scaffolding time)
Follow-up TODOs: None
-->

# Web App + MCP Server Constitution

## Core Principles

### I. MCP Protocol Compliance (NON-NEGOTIABLE)

The server MUST implement the official Model Context Protocol specification
exactly as published, with no proprietary extensions exposed by default.
Tool schemas, request/response envelopes, error codes, and lifecycle
semantics MUST conform to the spec version pinned in `plan.md`. Any
deviation MUST be feature-flagged off by default and documented as a
non-standard extension. **Rationale**: Interoperability with arbitrary
MCP clients is the entire point of the server; silent drift from the spec
breaks every consumer.

### II. Read-Only by Default

Every MCP tool that wraps a backing API endpoint MUST be read-only
unless explicitly opted into write mode via a per-tool, per-deployment
flag (e.g., `--allow-mutations` or env `MCP_ALLOW_MUTATIONS=true`).
MCP tools that map to HTTP verbs `POST`, `PUT`, `PATCH`, or `DELETE`
on the backing web app MUST refuse to execute when the flag is absent
and MUST return a structured error explaining how to enable them. Tools
mapping to `GET` and `HEAD` are exempt. The web app itself MUST still
enforce its own authorization independently — the MCP flag is defense
in depth, not the only gate. **Rationale**: AI clients hallucinate tool
calls; default-deny on mutations prevents an LLM from silently creating,
updating, or deleting application data through the MCP surface.

### III. Test-First (NON-NEGOTIABLE)

Every tool, handler, and protocol adapter MUST have a failing xUnit test
written and reviewed before any implementation code is committed.
Red-Green-Refactor is mandatory. Aggregate line coverage MUST be ≥ 80%;
PRs that lower coverage are blocked. Integration tests against a real
Kubernetes API (k3s via Testcontainers or kind) are REQUIRED for any
tool that talks to the cluster. **Rationale**: AI-generated handlers are
plausible-looking but easy to get subtly wrong; tests are the only
durable proof that behavior matches the spec.

### IV. Container-Native

The server MUST ship as a single Linux OCI image with no host
dependencies (no installed kubectl, no host volume mounts, no privileged
mode). The container MUST run as a non-root user with a read-only root
filesystem, MUST declare `runAsNonRoot: true` and
`allowPrivilegeEscalation: false` in its Pod spec, and MUST expose
`/healthz` and `/readyz` endpoints. The image MUST be reproducible
(pinned base image digest, deterministic build). **Rationale**:
Kubernetes-native deployment is the target environment; running as root
or relying on host state defeats multi-tenant isolation and breaks
supply-chain provenance.

### V. Zero-Secrets-in-Logs (NON-NEGOTIABLE)

Secrets, tokens, connection strings, bearer headers, Kubernetes `Secret`
values, and environment variables matching common credential patterns
(`*_TOKEN`, `*_KEY`, `*_PASSWORD`, `*_SECRET`, `AZURE_*`) MUST NEVER
appear in stdout, stderr, structured logs, OpenTelemetry spans, or
exception messages. A redaction middleware MUST run on every log sink
and MUST be covered by unit tests asserting redaction of known patterns.
Logs of K8s `Secret` resources MUST omit the `data` field entirely.
**Rationale**: An MCP server reads cluster state on behalf of AI
clients; leaking a Secret to a log aggregator is indistinguishable from
leaking it to the public internet.

## Security Requirements

Authentication to Azure and the AKS cluster MUST use **Azure Workload
Identity** (federated OIDC tokens projected into the pod). Service-
principal client secrets, certificates, and connection strings MUST NOT
be mounted, baked into images, or stored in environment variables.

In-cluster authorization MUST use a dedicated `ServiceAccount` bound to
a least-privilege `ClusterRole` (read-only verbs: `get`, `list`, `watch`)
across the namespaces the server is permitted to inspect. Write verbs
are bound only when Principle II's mutation flag is enabled at deploy
time, and even then only for the explicit resource kinds the enabled
tools need.

All inbound traffic MUST be TLS-terminated. The image MUST be scanned
for CVEs in CI; HIGH or CRITICAL findings block release unless an
exception is documented in `plan.md`.

## Deployment & Operations

### Service Topology

The system consists of **two deployable services**, each shipped as its
own OCI image and deployed as a separate Kubernetes `Deployment` in the
same AKS cluster:

1. **Web App** — owns business logic and data; exposes a versioned REST
   API (e.g., `/api/v1/...`). It is the source of truth and enforces
   authorization. It has no knowledge of MCP.
2. **MCP Server** — a thin adapter that exposes a curated subset of the
   Web App's API as MCP tools. It calls the Web App over in-cluster
   HTTP (`ClusterIP` Service) and forwards the caller's identity. It
   owns no data.

The two services MUST be independently buildable, testable, and
deployable. The MCP server MUST NOT bypass the Web App to reach
datastores directly. Breaking changes to the Web App's API MUST bump
the API version path; the MCP server pins a specific API version.

### Azure Targeting

All Azure infrastructure MUST be defined as **Bicep** modules checked
into the repository. Deployment MUST target **Azure Kubernetes Service**
and MUST be restricted to:

- **Subscription**: `d3c24b47-6f06-4152-8ade-6be38ba31c8c`
- **Resource Group**: `sainitesh-test`

Deployments to any other subscription or resource group are prohibited
unless this constitution is amended. CI/CD pipelines MUST fail-fast on
mismatched subscription or resource-group context.

Releases MUST be reproducible: each deployment is tied to an immutable
image digest and a Bicep parameter file committed to the repo. Rollback
MUST be possible by redeploying a previous digest without code changes.

Structured JSON logs MUST be emitted to stdout (12-factor). Metrics and
traces MUST be exported via OpenTelemetry. Alerting on `/readyz` failure
and on redaction-middleware errors is REQUIRED before any production
deploy.

## Governance

This constitution supersedes all other practices, style guides, and
individual preferences within this repository. When a `plan.md`,
`tasks.md`, or PR conflicts with a principle here, the constitution
wins and the conflicting artifact MUST be revised.

**Amendment process**: Changes require (a) a PR modifying this file,
(b) an updated Sync Impact Report at the top, (c) a version bump per
the policy below, and (d) approval from the repository owner. Any
amendment that removes or weakens a NON-NEGOTIABLE principle additionally
requires a documented migration plan for existing artifacts.

**Versioning policy** (semantic):

- **MAJOR**: a principle is removed, renamed in a breaking way, or
  weakened from NON-NEGOTIABLE to advisory.
- **MINOR**: a new principle or new mandatory section is added, or
  guidance is materially expanded.
- **PATCH**: clarifications, typos, wording refinements with no
  semantic change.

**Compliance review**: Every PR description MUST include a one-line
constitution-check statement. The `/speckit.plan` and `/speckit.analyze`
commands MUST be re-run whenever this file changes so dependent
artifacts stay aligned. Day-to-day implementation guidance lives in
`README.md` (to be created at project scaffolding time), not in this
constitution.

**Version**: 1.1.0 | **Ratified**: 2026-05-16 | **Last Amended**: 2026-05-16
