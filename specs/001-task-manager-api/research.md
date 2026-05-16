# Phase 0 — Research: Task Manager API & MCP Server

All `NEEDS CLARIFICATION` items from the Technical Context have been resolved below.

---

## 1. MCP C# SDK and pinned spec version

- **Decision**: Use the official **`ModelContextProtocol`** NuGet package (the Microsoft/Anthropic-maintained C# SDK) hosted via `Microsoft.Extensions.Hosting`. Pin to the SDK's `2025-06-18` MCP specification revision (the latest stable revision at the time of planning). The pinned version string is exposed by the server in its `initialize` response so clients can detect drift.
- **Transport**: HTTP streaming (SSE-based streamable HTTP) using the SDK's `WithHttpTransport()` builder. Rejected stdio because both services run as long-lived Kubernetes Deployments behind ClusterIP Services, and HTTP is the only sane transport across pods.
- **Rationale**: Official SDK is the only path that satisfies Principle I (no proprietary protocol deviation). HTTP transport matches the in-cluster topology.
- **Alternatives considered**: hand-rolled JSON-RPC over WebSockets (rejected — duplicates SDK work and risks drift); stdio (rejected — incompatible with K8s topology); third-party community SDKs (rejected — no provenance guarantee).

## 2. Polly v8 resilience pipeline

- **Decision**: Use `Microsoft.Extensions.Http.Resilience` (Polly v8) on the typed `TaskApiClient`. Pipeline:
  - **Total request timeout**: 5 s (matches SC-009 bound).
  - **Per-attempt timeout**: 2 s.
  - **Retry**: 3 attempts, exponential backoff with jitter (base 200 ms, max 1 s), only on `HttpRequestException`, 5xx, and 408/429.
  - **Circuit breaker**: open after 5 consecutive failures within 30 s; break duration 30 s; samples per the SDK defaults.
- **Rationale**: Bounded failure (SC-009) requires a hard total timeout. Retries cover transient PostgreSQL/Pod restarts. Circuit breaker prevents cascading failure when the Web App is genuinely down.
- **Alternatives considered**: Polly v7 (rejected — superseded; v8 ships in `Microsoft.Extensions.Http.Resilience`); no resilience at all (rejected — violates SC-009).

## 3. EF Core migration delivery

- **Decision**: Build the EF Core migrations bundle into the Web App image. A Kubernetes `Job` (`webapp-migrate`) runs the bundle to apply pending migrations before the Web App `Deployment` rolls out. The deploy workflow does `kubectl apply -f migrate-job.yaml && kubectl wait --for=condition=complete --timeout=180s job/webapp-migrate` then applies the rest of the overlay.
- **Rationale**: Keeps schema changes auditable, reversible (rollback by deploying a prior digest plus the matching prior Job), and decoupled from app startup (no race on multi-replica rollouts).
- **Alternatives considered**: Run migrations on app startup (rejected — multi-replica race; cannot block rollout on schema failure cleanly); a separate "migrator" image (rejected — adds an image to maintain for no benefit since the bundle is already in-image).

## 4. PostgreSQL authentication

- **Decision**: Use **Azure AD (Entra ID) token authentication** for PostgreSQL via Workload Identity. At connection time, the Web App calls `DefaultAzureCredential.GetTokenAsync("https://ossrdbms-aad.database.windows.net/.default")` and passes the resulting JWT as the Npgsql `Password`. The PostgreSQL server has an AAD admin set to the UAMI's principal id, with a database-level `aad_user` granted access to the `taskmgr` database.
- **Rationale**: Satisfies the constitution's "no SP secrets anywhere"; no static DB password is ever stored, mounted, or logged. Tokens are short-lived (≈1 hour) and refreshed in-process by Azure.Identity.
- **Alternatives considered**: Static password from Key Vault CSI (rejected — still a long-lived secret); managed-identity password rotation via AKV (rejected — adds infra without removing the secret class).

## 5. AKS reuse-or-create algorithm

- **Decision**: `scripts/aks-discover.sh` runs `az aks list -g sainitesh-test -o json`. If any cluster exists, the first cluster (sorted by name) is selected and its `name`, `oidcIssuerProfile.issuerUrl`, and `nodeResourceGroup` are written to `infra/aks.discovered.json`. The Bicep deployment reads this file; if it indicates an existing cluster, the `aks.bicep` module is skipped (`enableAksCreation = false`) and only the federated credential module is invoked. If no cluster is found, `aks.bicep` is invoked to create one with the **cost-optimized v1 profile**: AKS **Free** control-plane tier, single system nodepool of `Standard_B2s` × 1–2 (cluster autoscaler enabled, min=1 max=2), OIDC issuer enabled, Workload Identity enabled, Azure CNI Overlay, managed identity, Container Insights add-on with 30-day Log Analytics retention.
- **Rationale**: Demos commonly run against existing infra; never blowing away a present cluster is the safer default.
- **Alternatives considered**: Always create (rejected — destructive and slow); Terraform import dance (rejected — out of scope; Bicep is mandated).

## 6. Workload Identity wiring

- **Decision**: One `Microsoft.ManagedIdentity/userAssignedIdentities` resource (`uami-taskmgr`). One `federatedIdentityCredentials` child resource binding it to `system:serviceaccount:taskmgr:taskmgr-sa` with the discovered AKS OIDC issuer URL as the `issuer`. The K8s `ServiceAccount` is annotated `azure.workload.identity/client-id: <uami-clientId>`. Pod templates carry the label `azure.workload.identity/use: "true"` and reference `serviceAccountName: taskmgr-sa`. Role assignments granted to the UAMI: `AcrPull` on the ACR (so the kubelet plugin can pull images), and the PostgreSQL AAD admin role for DB auth.
- **Rationale**: This is the documented happy path for AKS Workload Identity since 2023; it eliminates every class of stored secret for Azure-side auth.
- **Alternatives considered**: AAD Pod Identity (rejected — deprecated); kubelet identity (rejected — coarse-grained, shared across all pods on the node).

## 7. Redaction patterns

- **Decision**: The redaction policy redacts on two axes:
  - **Key names** (case-insensitive substring match): `password`, `secret`, `token`, `key`, `connectionstring`, `apikey`, `authorization`, `cookie`, `x-api-key`, and any env-var name matching the regex `^(.*_)?(TOKEN|KEY|PASSWORD|SECRET)$` or `^AZURE_.*$`.
  - **Value patterns** (regex over string values): JWT-shaped (`eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}`), bearer headers (`(?i)^Bearer\s+\S+`), Postgres connection strings (`(?i)(Host|Server)=.+;.*(Password|Pwd)=`), generic key=value secret pairs in URLs (`[?&](api[_-]?key|token|password)=[^&]+`).
  - Replacement: `***REDACTED***`. Number of redactions per `LogEvent` is emitted as the OTel counter `redaction_failures_total` only when a redaction was *attempted but failed* (used by the alert rule); successful redactions increment a separate `redaction_total` counter.
- **Rationale**: Belt-and-braces — even if a developer logs a config object or an exception with a connection string, the value never reaches the sink.
- **Alternatives considered**: Whitelist-only logging (rejected — too easy to break debuggability); ML-based PII detection (rejected — overkill, slow, non-deterministic).

## 8. Correlation-id contract

- **Decision**: Header name **`X-Correlation-Id`** (case-insensitive on read, canonical on write). Value format: ULID (Crockford-base32, 26 chars) preferred for sortability; UUIDv4 also accepted on input. The MCP server:
  - On inbound MCP `tools/call`, reads correlation id from `_meta.correlationId` in the request envelope if present; otherwise generates a new ULID.
  - Adds it to the outbound HTTP call via `CorrelationIdHandler` (`DelegatingHandler` in the typed-client pipeline).
  - Includes it in every log scope (Serilog `LogContext.PushProperty("CorrelationId", id)`) and in the OTel span as attribute `correlation.id`.
  - Echoes it back to the MCP client in the `_meta.correlationId` field of every tool response.
- The Web App's `CorrelationIdMiddleware` reads the inbound header (generating one if absent), enriches Serilog/OTel identically, and echoes it on the response header.
- **Rationale**: SC-006 requires end-to-end trace reconstruction across MCP → HTTP → DB; a single propagated id is the minimum sufficient mechanism. ULID is sortable and grep-friendly in logs.
- **Alternatives considered**: W3C `traceparent` only (rejected — OTel already handles spans; `X-Correlation-Id` survives across sampling decisions and is human-grep-able); GUID (accepted on input but not preferred on generation due to non-sortability).

## 9. Test sequencing

- **Decision**: `tasks.md` (Phase 2 output) will be generated in strict TDD order. For every implementation slice, the corresponding task in `tests/WebApp.Tests` or `tests/McpServer.Tests` is listed *before* its `src/*` counterpart. The first tasks in the project bring up the empty test projects (so `dotnet test` runs and fails) before any production code is written.
- **Rationale**: Principle III is NON-NEGOTIABLE and mandates failing tests before code.
- **Alternatives considered**: None — principle is non-negotiable.

## 10. NetworkPolicy shape

- **Decision**: Three NetworkPolicies in `deploy/base/`:
  1. **`default-deny`** in namespace `taskmgr` denying all ingress and egress by default.
  2. **`mcp-egress`** allowing the MCP server pod (`podSelector: app=mcp-server`) egress to:
     - The Web App `Service` (`podSelector: app=webapp` on TCP 8080),
     - `kube-dns` in `kube-system` on UDP/TCP 53,
     - Azure Monitor OTel collector endpoint (egress rule by IP block from the AKS subnet's allowed Monitor IPs, or via a sidecar/daemonset collector — chosen because Container Insights deploys a node-local OTel collector).
  3. **`webapp-ingress`** allowing the Web App pod ingress from `app=mcp-server` on TCP 8080 (plus DNS).
- **Rationale**: Default-deny is the only safe baseline. The MCP server has no business calling anything except the Web App (and DNS/telemetry) — this enforces topology principle.
- **Alternatives considered**: Cilium L7 policies (rejected — adds a CNI dependency the cluster may not have); namespace-level only (rejected — too coarse; would allow MCP server to call sibling deployments).

---

## Cross-cutting reaffirmations

- **TLS-terminated inbound**: Not applicable in v1 (no public ingress). When added, ingress controller (NGINX or AGIC) terminates TLS; this is captured as a deferred decision so principle Sec/TLS will be re-checked when ingress is introduced.
- **Image reproducibility**: `infra/base-images.lock` records `mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:<digest>` and is the only place either Dockerfile names a base image (Dockerfiles `ARG BASE_IMAGE` from this file).
- **No outstanding clarifications**: Every Technical Context entry resolved.

## 11. Azure region selection

- **Decision**: Deploy region is **`eastus`** for v1. Rationale: (a) the target RG `sainitesh-test` is *already* located in `eastus` — deploying anywhere else would create cross-region data-plane traffic and an orphan empty RG; (b) `eastus` is in the subscription's recommended-physical region list (verified via `az account list-locations --query "[?metadata.regionCategory=='Recommended'].name"`); (c) all chosen SKUs (`Standard_B2s` VMs, PostgreSQL Flexible `Burstable_B1ms`, ACR Basic, Log Analytics PerGB2018, AKS Free control plane) are GA in `eastus`.
- **Enforcement**: `scripts/assert-region-availability.sh <region>` runs in CI before any Bicep deployment and fails fast if **any** of the following return restricted/unavailable in the chosen region: `Standard_B2s` VM SKU (`az vm list-skus --location <region> --size Standard_B2s`), PostgreSQL Flexible `Standard_B1ms` (`az postgres flexible-server list-skus --location <region>`), AKS managed cluster (`az provider show -n Microsoft.ContainerService --query "resourceTypes[?resourceType=='managedClusters'].locations"`). The region is exposed as a single Bicep parameter `location` (default `eastus`) sourced from `infra/params/<env>.bicepparam`, so changing it is a one-line edit + automatic re-verification by the assert script.
- **Alternatives considered**: `eastus2` / `westus2` / `centralus` (all in the recommended list and would work) — rejected to avoid orphaning the existing RG and to keep all resources in one region for the demo; `westeurope`/`northeurope` — rejected for higher AKS+Postgres cost than US regions at the chosen tiers.

