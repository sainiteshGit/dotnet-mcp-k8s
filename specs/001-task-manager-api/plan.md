# Implementation Plan: Task Manager API & MCP Server

**Branch**: `001-task-manager-api` | **Date**: 2026-05-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from [specs/001-task-manager-api/spec.md](./spec.md)

## Summary

Deliver two cooperating .NET 10 LTS services to AKS, packaged as separate OCI images and separate Kubernetes `Deployment`s in the same cluster:

- **Web App** — ASP.NET Core Minimal APIs over EF Core 10 + PostgreSQL (Azure Database for PostgreSQL Flexible Server), exposing a versioned `/api/v1/` REST surface for the `Task` resource (full CRUD, filtering, pagination, uniform error envelope). It is the source of truth.
- **MCP Server** — .NET 10 console host using the official `ModelContextProtocol` C# SDK, MCP spec **`2025-06-18`** pinned, HTTP streaming transport. Exposes exactly six tools (`create_task`, `list_tasks`, `get_task`, `update_task_status`, `update_task_priority`, `delete_task`) that call the Web App through a typed `HttpClient` with Polly v8 (timeout + retry + circuit breaker) and propagate `X-Correlation-Id` end-to-end.

Both images are built from `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` pinned by digest, run as non-root uid `10001` with `readOnlyRootFilesystem`, expose `/healthz` and `/readyz`, and are scanned by Trivy in CI. Infrastructure is **Bicep**, deployment is **Kustomize** (`base` + `overlays/dev` + `overlays/prod`). Auth to Azure (and Postgres) is **Azure Workload Identity** (UAMI + federated credential to a K8s `ServiceAccount`); CI pushes/deploys via **GitHub OIDC** to the same UAMI — no stored secrets anywhere. All Azure work is restricted to the subscription / resource group supplied via the `AZURE_SUBSCRIPTION_ID` and `AZURE_RESOURCE_GROUP` env vars (CI repository variables; never committed) and enforced by `scripts/assert-azure-context.sh`.

## Technical Context

**Language/Version**: C# / .NET SDK 10.0.x (LTS). Both services target `net10.0`.

**Primary Dependencies**:

- Web App: ASP.NET Core Minimal APIs, Entity Framework Core 9 + `Npgsql.EntityFrameworkCore.PostgreSQL`, FluentValidation, Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.Console` with JSON formatter, a custom redaction enricher), OpenTelemetry (`OpenTelemetry.Extensions.Hosting`, ASP.NET Core + HttpClient + EF Core + OTLP exporters), Swashbuckle (OpenAPI 3.1), `Azure.Identity` (for Entra ID token-based Postgres auth).
- MCP Server: `ModelContextProtocol` (official C# SDK, MCP spec `2025-06-18`), `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience` (Polly v8), Serilog (same redaction enricher), OpenTelemetry, `Azure.Identity`.

**Storage**: Azure Database for PostgreSQL Flexible Server, **Burstable B1ms**, Entra ID auth only (no admin password). Schema owned by the Web App via EF Core migrations applied by a Kubernetes `Job` (`webapp-migrate`) before the Web App `Deployment` rolls out. The MCP server holds no data.

**Testing**:

- `tests/WebApp.Tests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) for handler/contract tests; **Testcontainers** (`Testcontainers.PostgreSql`) for integration tests against a real Postgres instance.
- `tests/McpServer.Tests` — xUnit + `WireMock.Net` to fake the Web App; MCP SDK in-memory transport for protocol-level tests.
- Aggregate line coverage gate **≥ 80 %** (Principle III); CI fails on regression.

**Target Platform**: Linux/amd64 Kubernetes pods on Azure Kubernetes Service. Base image `mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:<digest>` (single source of truth: `infra/base-images.lock`). No host dependencies; no privileged mode; no host volume mounts.

**Project Type**: Polyglot **monorepo** containing two deployable services (web service + console/MCP service), shared infra and deploy assets. Repository layout in *Project Structure* below.

**Performance Goals**: `GET /api/v1/tasks` returns a page of ≤ 20 items in **< 300 ms p95** under 50 concurrent clients with a 10 000-row dataset (SC-003). `POST /api/v1/tasks` **< 200 ms p95** under the same load (SC-004). Every MCP tool returns within **5 s** even when the Web App is unreachable (SC-009).

**Constraints**:

- Two independently buildable/testable/deployable images.
- Pod security: `runAsNonRoot=true`, `runAsUser=10001`, `runAsGroup=10001`, `fsGroup=10001`, `allowPrivilegeEscalation=false`, `readOnlyRootFilesystem=true`, `capabilities.drop=["ALL"]`, `seccompProfile.type=RuntimeDefault`. Writable `emptyDir` mount at `/tmp` only.
- `/healthz` (liveness) and `/readyz` (readiness, includes DB reachability for the Web App and backing-API reachability for the MCP server).
- Zero static secrets: no SP passwords, no static DB passwords, no GitHub Actions client-secret values — Workload Identity + GitHub OIDC only.
- MCP spec **`2025-06-18`** pinned and asserted by a test that reads the SDK's advertised protocol version.
- `NetworkPolicy` default-deny in the `taskmgr` namespace; MCP server pod egress allowed only to the Web App `ClusterIP` Service (+ DNS + telemetry collector).
- Dev overlay sets `MCP_ALLOW_MUTATIONS=true`; prod overlay omits it. `scripts/lint-prod-overlay.sh` (wired into CI) fails the build if the prod overlay ever sets it to `true`.
- All Azure resources live in the subscription / RG supplied via the `AZURE_SUBSCRIPTION_ID` and `AZURE_RESOURCE_GROUP` env vars; `scripts/assert-azure-context.sh` runs first in CI and fails fast on mismatch.

**Scale/Scope**: v1 demo footprint — ~10 000 tasks, one Web App `Deployment` (2 replicas), one MCP server `Deployment` (1–2 replicas), single Postgres Flexible Server (B1ms). Designed so a future auth layer can be added in front of the API without changing the v1 contract (FR-024, SC-007).
**Cost-Optimized v1 SKU Profile** (cheapest viable tier for every resource; lock-in decision — changing a SKU upward requires a plan amendment):

| Resource | Tier / SKU | Notes |
|---|---|---|
| **Region** | **`eastus`** | Matches existing `sainitesh-test` RG; verified in subscription's Recommended region list; all SKUs below are GA here. Re-verified at deploy time by `scripts/assert-region-availability.sh`. |
| AKS control plane | **Free** tier | $0/mo; SLO not SLA — fine for v1 demo |
| AKS nodepool | `Standard_B2s` × 1–2 (autoscale) | Burstable, ~$30/mo per node when running |
| ACR | **Basic** tier | 10 GiB included; sufficient for two images |
| PostgreSQL Flexible Server | **Burstable** `B1ms` + 32 GiB Premium SSD | Cheapest Flexible tier; Entra ID auth only |
| Log Analytics workspace | `PerGB2018`, **30-day retention** | Pay-per-GB; demo volume ~$5/mo |
| Application Insights | Workspace-based (no separate billing) | Costs roll into LAW |
| User-Assigned Managed Identity | n/a | Free |
| Federated Identity Credentials | n/a | Free |
| Metric/log alert rules | Standard | ~$0.10/mo per rule |
| Action Group | Reuse existing `Application Insights Smart Detection` in `sainitesh-test` | $0 incremental |

Rough total when actively running: **~$70–100/mo**. Park strategy: `az aks stop` + scale Postgres compute to minimum = ~$10–15/mo idle.
## Constitution Check

*GATE: passed before Phase 0 research. Re-checked post-Phase 1 below.*

Binding constitution: [.specify/memory/constitution.md](../../.specify/memory/constitution.md) (**v1.1.0**, ratified 2026-05-16).

| # | Principle | Status | How this plan satisfies it |
|---|---|---|---|
| I | **MCP Protocol Compliance (NON-NEGOTIABLE)** | PASS | Use the official `ModelContextProtocol` C# SDK only. MCP spec version **`2025-06-18`** is pinned in plan and asserted by a unit test that reads the SDK's advertised `ProtocolVersion` from the `initialize` response. No proprietary tools or fields; `_meta.correlationId` is a standard MCP `_meta` field. |
| II | **Read-Only by Default** | EXCEPTION (scoped, mechanically enforced — see *Complexity Tracking*) | All four mutation tools (`create_task`, `update_task_status`, `update_task_priority`, `delete_task`) refuse with a structured `mutations_disabled` error when `MCP_ALLOW_MUTATIONS != "true"` (see [contracts/mcp-tools.md](./contracts/mcp-tools.md)). Dev overlay enables mutations (explicit demo intent); **prod overlay leaves it unset / effectively false** and `scripts/lint-prod-overlay.sh` fails CI if prod ever flips it on. The Web App enforces its own validation independently (defence in depth). |
| III | **Test-First (NON-NEGOTIABLE)** | PASS | `tasks.md` will list **`tests/WebApp.Tests`** and **`tests/McpServer.Tests`** scaffolding tasks (and per-feature failing tests) *before* the corresponding `src/WebApp` and `src/McpServer` implementation tasks. Coverage gate ≥ 80 % wired into CI; PRs that lower coverage are blocked. |
| IV | **Container-Native** | PASS | Multi-stage Dockerfile per service, base `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` pinned by digest in `infra/base-images.lock`. Pod `securityContext`: `runAsNonRoot=true`, `runAsUser=10001`, `runAsGroup=10001`, `allowPrivilegeEscalation=false`, `readOnlyRootFilesystem=true`, `capabilities.drop=["ALL"]`. `/healthz` and `/readyz` exposed on both services. No host mounts, no privileged, no kubectl-on-host. |
| V | **Zero-Secrets-in-Logs (NON-NEGOTIABLE)** | PASS | Serilog redaction enricher with the key-list + regex policy in [research.md §7](./research.md) (covers `*_TOKEN`, `*_KEY`, `*_PASSWORD`, `*_SECRET`, `AZURE_*`, `Authorization: Bearer …`, JWT-shaped values, Postgres connection strings, K8s `Secret.data`). Unit tests in `tests/WebApp.Tests/Logging/RedactionTests.cs` and `tests/McpServer.Tests/Logging/RedactionTests.cs` assert redaction for every documented pattern (failing-first per Principle III). |
| — | **Security: Workload Identity** | PASS | One UAMI (`uami-taskmgr`) with one `FederatedIdentityCredentials` binding it to `system:serviceaccount:taskmgr:taskmgr-sa` (AKS OIDC issuer). The K8s `ServiceAccount` is annotated `azure.workload.identity/client-id: <uami-clientId>`. Pods carry `azure.workload.identity/use: "true"`. Postgres uses Entra ID token auth via `DefaultAzureCredential` — **no static DB password anywhere**. GitHub Actions uses OIDC federation to the same UAMI for `az login` / `acr push` / `kubectl apply` — **no `AZURE_CLIENT_SECRET`**. |
| — | **Security: Authorization** | PASS (v1 scope) | API is in-cluster only (no public ingress in v1); FR-024 keeps the contract auth-additive. `NetworkPolicy` default-deny + MCP-server-egress-to-Web-App-only enforces the topology authorization. The UAMI's Azure-side role assignments are least-privilege (`AcrPull` on ACR, Postgres AAD admin only). |
| — | **Security: TLS + CVE scan** | PASS | No public ingress in v1, so TLS termination is a deferred decision (recorded in [research.md](./research.md)). Trivy scan of both images gates the CI pipeline; HIGH/CRITICAL findings block release. |
| — | **Deployment: Service Topology** | PASS | Two separate `Deployment`s, separate `Service`s, separate images. MCP server never reaches Postgres directly; it only calls the Web App's `/api/v1/` over `ClusterIP`. The Web App pins no MCP code or knowledge. |
| — | **Deployment: Azure Targeting** | PASS | All infra is **Bicep** in `infra/` (modules: `uami.bicep`, `acr.bicep`, `postgres.bicep`, `loganalytics.bicep`, `aks.bicep`, `main.bicep`). `scripts/assert-azure-context.sh` runs first in CI and fails fast if `az account show --query id -o tsv` ≠ `$AZURE_SUBSCRIPTION_ID` or if the deployment target RG ≠ `$AZURE_RESOURCE_GROUP`. AKS reuse-or-create algorithm in [research.md §5](./research.md). |
| — | **Governance: per-PR constitution-check line** | PASS | PR template carries the required one-line constitution-check statement (added in `.github/PULL_REQUEST_TEMPLATE.md` during scaffolding). |

**Initial gate verdict**: passed. The one exception (Principle II in the dev overlay) is documented, scoped to a single env-var flag in a single overlay, mechanically enforced against prod by `scripts/lint-prod-overlay.sh`, and recorded in *Complexity Tracking* below.

**Post-Phase-1 re-evaluation**: Phase 1 artifacts ([data-model.md](./data-model.md), [contracts/webapp-openapi.yaml](./contracts/webapp-openapi.yaml), [contracts/mcp-tools.md](./contracts/mcp-tools.md), [quickstart.md](./quickstart.md)) were generated and reviewed against the table above. No new violations introduced; all gates remain green. The mutation-gate contract in [contracts/mcp-tools.md](./contracts/mcp-tools.md) makes Principle II's exception explicit at the contract layer (not just at runtime), and the OpenAPI contract's uniform `ErrorEnvelope` plus `/api/v1/` prefix satisfy FR-020 through FR-024, which preserve future auth additivity.

## Project Structure

### Documentation (this feature)

```text
specs/001-task-manager-api/
├── plan.md                   # this file
├── research.md               # Phase 0 output
├── data-model.md             # Phase 1 output
├── quickstart.md             # Phase 1 output
├── contracts/
│   ├── webapp-openapi.yaml   # Phase 1 — Web App REST contract (OpenAPI 3.1)
│   └── mcp-tools.md          # Phase 1 — MCP tool surface contract
├── checklists/
│   └── requirements.md       # pre-existing requirements checklist
└── tasks.md                  # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── WebApp/                  # ASP.NET Core Minimal APIs + EF Core + Postgres
│   ├── Api/                 # Endpoint groups, request/response DTOs, error mapping
│   ├── Domain/              # TaskItem entity, enums, domain services
│   ├── Persistence/         # DbContext, EF Core configuration, migrations
│   ├── Validation/          # FluentValidation validators (mirror data-model.md rules)
│   ├── Observability/       # Serilog config, redaction enricher, OTel wiring
│   ├── HealthChecks/        # /healthz, /readyz
│   ├── Program.cs
│   └── WebApp.csproj
└── McpServer/               # .NET 10 console host using ModelContextProtocol SDK
    ├── Tools/               # One file per tool (create_task, list_tasks, …)
    ├── Backing/             # Typed TaskApiClient + DTOs (mirrors webapp-openapi.yaml)
    ├── Pipeline/            # CorrelationIdHandler + Polly resilience pipeline config
    ├── Mutation/            # MutationGate (reads MCP_ALLOW_MUTATIONS), structured error mapper
    ├── Observability/       # Serilog + redaction (shared policy with WebApp), OTel
    ├── HealthChecks/        # /healthz, /readyz
    ├── Program.cs
    └── McpServer.csproj

tests/
├── WebApp.Tests/
│   ├── Api/                 # Handler + contract tests via WebApplicationFactory
│   ├── Validation/          # FluentValidation rule tests
│   ├── Integration/         # Testcontainers Postgres end-to-end tests
│   ├── Logging/             # RedactionTests (Principle V)
│   └── WebApp.Tests.csproj
└── McpServer.Tests/
    ├── Tools/               # Per-tool tests using WireMock.Net
    ├── Mutation/            # MUTATIONS_DISABLED path tests
    ├── Pipeline/            # CorrelationIdHandler + Polly tests
    ├── Protocol/            # MCP SDK in-memory transport tests; pinned-version assertion
    ├── Logging/             # RedactionTests (Principle V)
    └── McpServer.Tests.csproj

infra/                       # Bicep
├── main.bicep
├── modules/
│   ├── uami.bicep           # UAMI + federated credential to taskmgr-sa
│   ├── acr.bicep            # ACR Basic tier + AcrPull role assignment to UAMI
│   ├── postgres.bicep       # Flexible Server, Burstable B1ms, Entra ID admin only
│   ├── loganalytics.bicep   # Log Analytics (PerGB2018, 30-day retention) + App Insights
│   └── aks.bicep            # Free control plane + Standard_B2s × 1–2 autoscale
│                            # (created only if scripts/aks-discover.sh found none)
├── params/
│   ├── dev.bicepparam
│   └── prod.bicepparam
├── aks.discovered.json      # Written by scripts/aks-discover.sh
└── base-images.lock         # Pinned base image digest (single source of truth)

deploy/                      # Kustomize
├── base/
│   ├── namespace.yaml
│   ├── serviceaccount.yaml      # taskmgr-sa, annotated with UAMI client-id
│   ├── webapp-deployment.yaml
│   ├── webapp-service.yaml
│   ├── webapp-migrate-job.yaml
│   ├── mcpserver-deployment.yaml
│   ├── mcpserver-service.yaml
│   ├── networkpolicy-default-deny.yaml
│   ├── networkpolicy-mcp-egress.yaml
│   ├── networkpolicy-webapp-ingress.yaml
│   └── kustomization.yaml
└── overlays/
    ├── dev/
    │   ├── mcp-allow-mutations-patch.yaml   # MCP_ALLOW_MUTATIONS=true
    │   └── kustomization.yaml
    └── prod/
        └── kustomization.yaml                # NO MCP_ALLOW_MUTATIONS patch; linted by CI

scripts/
├── assert-azure-context.sh        # Fails if not $AZURE_SUBSCRIPTION_ID / $AZURE_RESOURCE_GROUP
├── aks-discover.sh                # Writes infra/aks.discovered.json
└── lint-prod-overlay.sh           # Fails if prod overlay sets MCP_ALLOW_MUTATIONS=true

.github/workflows/
└── ci.yml                         # build → unit → integration → trivy → push → deploy (OIDC)

Dockerfile.webapp                  # Multi-stage; ARG BASE_IMAGE from infra/base-images.lock
Dockerfile.mcpserver               # Multi-stage; same base
.dockerignore
TaskManager.sln
```

**Structure Decision**: monorepo with two service projects (`src/WebApp`, `src/McpServer`), two matching test projects (`tests/WebApp.Tests`, `tests/McpServer.Tests`), shared infra (`infra/`, Bicep), shared deploy (`deploy/`, Kustomize), and CI/scripts at the root. This is the smallest layout that keeps the two images independently buildable, testable, and deployable per the *Service Topology* section of the constitution, while keeping observability, security, and resilience code reviewable side-by-side.

## Complexity Tracking

> Justifications for deliberate constitutional deviations. Every entry is mechanically enforced so it cannot silently expand.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| **Principle II exception — `MCP_ALLOW_MUTATIONS=true` in `deploy/overlays/dev` only** | The entire purpose of this demo is to show an AI agent driving end-to-end task lifecycle changes (create / update status / update priority / delete) through MCP. A read-only dev overlay would make the demo unable to demonstrate the value proposition that motivates the system (User Story 2, acceptance scenarios 1, 4, 6). The exception is scoped to a single env-var flag in a single overlay (`deploy/overlays/dev`), explicitly documented in [contracts/mcp-tools.md § Mutation gate](./contracts/mcp-tools.md), and **`scripts/lint-prod-overlay.sh` (wired into CI) fails the build if the prod overlay ever sets `MCP_ALLOW_MUTATIONS=true`**. A failing test (`tests/McpServer.Tests/Mutation/MutationGateTests.cs`) asserts the `mutations_disabled` response shape when the flag is unset/false. | A truly read-only MCP surface (the simpler alternative) cannot satisfy User Story 2's acceptance scenarios for create/update/delete. A per-tool RBAC system in the MCP server (a more elaborate alternative) was rejected because the constitution's flag-based mechanism is already sufficient and is the documented standard escape hatch. |
| **TLS waiver — no inbound TLS termination in v1** | v1 has **no public ingress**: both Services are `ClusterIP` and the MCP server only reaches the Web App in-cluster via `http://webapp.taskmgr.svc.cluster.local`. The constitution's TLS requirement applies to *inbound* traffic; with no inbound surface there is nothing to terminate. Pod-to-pod traffic is restricted to the MCP→Web App path by `NetworkPolicy` (T110–T113), which is the same threat-model boundary TLS would protect against here. When public ingress is added in a future release, NGINX/AGIC will terminate TLS at the edge and this waiver expires. A startup assertion in the Web App refuses to bind a non-loopback `Kestrel` listener on a public IP (defence in depth). | Self-signed in-cluster mTLS (the simpler alternative) doubles operational complexity (cert rotation, trust store distribution) without changing the threat model in a single-tenant cluster with default-deny NetworkPolicy. A service mesh (Linkerd/Istio) was rejected as massive overkill for two pods. |

