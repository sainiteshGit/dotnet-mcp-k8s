# Tasks: Task Manager API & MCP Server

**Input**: Design documents in `specs/001-task-manager-api/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/webapp-openapi.yaml](./contracts/webapp-openapi.yaml), [contracts/mcp-tools.md](./contracts/mcp-tools.md), [quickstart.md](./quickstart.md)
**Binding constitution**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v1.1.0.

**Tests**: REQUIRED. Principle III (Test-First, NON-NEGOTIABLE) — every test task is ordered BEFORE its corresponding implementation task, and tests MUST be observed failing before implementation begins.

**Organization**: Tasks are grouped by user story (P1 → P2 → P3). Within each story: setup/scaffolding → failing tests → implementation → integration → docker → kustomize → bicep → CI wiring.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no ordering dependency on incomplete tasks)
- **[Story]**: `[US1]`, `[US2]`, `[US3]` — maps to user stories in [spec.md](./spec.md)
- Setup, Foundational, and Polish phases carry no story label

## Path Conventions

- Source: `src/WebApp/`, `src/McpServer/`
- Tests: `tests/WebApp.Tests/`, `tests/McpServer.Tests/`
- Infra (Bicep): `infra/`
- Deploy (Kustomize): `deploy/base/`, `deploy/overlays/{dev,prod}/`
- Scripts: `scripts/`
- CI: `.github/workflows/ci.yml`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository scaffolding, base image pinning, Azure-context guard, prod-overlay linter, PR template. No story-specific code.

- [X] T001 Create `TaskManager.sln` at repo root with empty solution scaffold
- [X] T002 [P] Create `.gitignore` (dotnet + node + python + IDE noise) and `.dockerignore` (excludes `bin/`, `obj/`, `tests/`, `.git/`, `infra/`, `deploy/`, `specs/`)
- [X] T003 [P] Create `.editorconfig` with C# formatting rules (4-space indent, file-scoped namespaces, nullable enabled)
- [X] T004 [P] Create `Directory.Packages.props` enabling Central Package Management for the solution
- [X] T005 [P] Create `Directory.Build.props` setting `TargetFramework=net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`
- [X] T006 [P] Create [scripts/assert-azure-context.sh](../../scripts/assert-azure-context.sh) that fails fast if `az account show --query id -o tsv` ≠ `d3c24b47-6f06-4152-8ade-6be38ba31c8c` or if the target RG argument ≠ `sainitesh-test`; make executable
- [X] T006a [P] Create [scripts/assert-region-availability.sh](../../scripts/assert-region-availability.sh) that takes a region arg (default `eastus`) and fails fast if any of: (a) the region is missing from `az account list-locations --query "[?metadata.regionCategory=='Recommended'].name"`; (b) `az vm list-skus --location <region> --size Standard_B2s` reports any non-empty `restrictions` array; (c) `az postgres flexible-server list-skus --location <region>` does not contain `Standard_B1ms`; (d) `Microsoft.ContainerService/managedClusters` is not listed for the region (`az provider show -n Microsoft.ContainerService --query "resourceTypes[?resourceType=='managedClusters'].locations"`). Make executable; wired into CI as the second job after `assert-context` and before any Bicep deploy.
- [X] T007 [P] Create [scripts/lint-prod-overlay.sh](../../scripts/lint-prod-overlay.sh) that greps `deploy/overlays/prod/` and exits non-zero if any file sets `MCP_ALLOW_MUTATIONS=true` (case-insensitive); make executable
- [X] T008 [P] Create [scripts/aks-discover.sh](../../scripts/aks-discover.sh) implementing the AKS reuse-or-create discovery from [research.md](./research.md) §5 and writing `infra/aks.discovered.json`; make executable
- [X] T009 [P] Create [infra/base-images.lock](../../infra/base-images.lock) pinning `mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:<digest>` as the single source of truth for base images
- [X] T010 [P] Create [.github/PULL_REQUEST_TEMPLATE.md](../../.github/PULL_REQUEST_TEMPLATE.md) including the mandatory per-PR constitution-check line
- [X] T011 [P] Create skeleton [.github/workflows/ci.yml](../../.github/workflows/ci.yml) with empty jobs `assert-context`, `build`, `unit-tests`, `integration-tests`, `trivy-scan`, `push`, `deploy` (jobs wired by later story phases)
- [X] T012 First job of ci.yml invokes `scripts/assert-azure-context.sh sainitesh-test` and blocks all downstream jobs on its success
- [X] T012a Second job of ci.yml invokes `scripts/assert-region-availability.sh eastus` (region read from `infra/params/<env>.bicepparam`); blocks all infra/deploy jobs on its success
- [X] T012b Create [scripts/assert-providers-registered.sh](../../scripts/assert-providers-registered.sh) that fails fast if any of these resource providers is not in `Registered` state in the target subscription: `Microsoft.ContainerService`, `Microsoft.ContainerRegistry`, `Microsoft.DBforPostgreSQL`, `Microsoft.OperationalInsights`, `Microsoft.Insights`, `Microsoft.AlertsManagement`, `Microsoft.ManagedIdentity`, `Microsoft.Network`, `Microsoft.Compute`, `Microsoft.Storage`, `Microsoft.Authorization`, `Microsoft.OperationsManagement`. Script lists unregistered providers in the failure message with the exact `az provider register -n <NAME>` command to run. Make executable; wire as third ci.yml job after `assert-region`, blocks all infra/deploy jobs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Project skeletons, shared Serilog redaction enricher (Principle V), shared error envelope and correlation-id primitives, shared OTel wiring, shared Azure infra (UAMI, ACR, Log Analytics) needed by every Deployment.

**⚠️ CRITICAL**: No user story work may begin until this phase is complete.

### Scaffolding

- [ ] T013 [P] `dotnet new webapi -n WebApp -o src/WebApp` (Minimal APIs template), add to solution
- [ ] T014 [P] `dotnet new console -n McpServer -o src/McpServer` (.NET 10), add to solution
- [ ] T015 [P] `dotnet new xunit -n WebApp.Tests -o tests/WebApp.Tests`, add to solution; reference `src/WebApp/WebApp.csproj`
- [ ] T016 [P] `dotnet new xunit -n McpServer.Tests -o tests/McpServer.Tests`, add to solution; reference `src/McpServer/McpServer.csproj`
- [ ] T017 [P] Add NuGet packages to `src/WebApp/WebApp.csproj`: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `FluentValidation.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.EntityFrameworkCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Swashbuckle.AspNetCore`, `Azure.Identity`
- [ ] T018 [P] Add NuGet packages to `src/McpServer/McpServer.csproj`: `ModelContextProtocol` (MCP spec `2025-06-18`), `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Azure.Identity`
- [ ] T019 [P] Add test packages to `tests/WebApp.Tests/WebApp.Tests.csproj`: `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `FluentAssertions`, `coverlet.collector`
- [ ] T020 [P] Add test packages to `tests/McpServer.Tests/McpServer.Tests.csproj`: `WireMock.Net`, `ModelContextProtocol` (in-memory transport), `FluentAssertions`, `coverlet.collector`

### Failing tests for shared infrastructure (Principle III)

- [ ] T021 [P] Write failing unit tests in [tests/WebApp.Tests/Logging/RedactionTests.cs](../../tests/WebApp.Tests/Logging/RedactionTests.cs) asserting redaction for EVERY pattern in [research.md](./research.md) §7: keys matching `*_TOKEN`, `*_KEY`, `*_PASSWORD`, `*_SECRET`, `AZURE_*`; `Authorization: Bearer …`; JWT-shaped values; Postgres connection strings; K8s `Secret.data`
- [ ] T022 [P] Write failing unit tests in [tests/McpServer.Tests/Logging/RedactionTests.cs](../../tests/McpServer.Tests/Logging/RedactionTests.cs) with the same coverage as T021 (shared redaction policy)
- [ ] T023 [P] Write failing unit tests in [tests/WebApp.Tests/Api/ErrorEnvelopeTests.cs](../../tests/WebApp.Tests/Api/ErrorEnvelopeTests.cs) asserting the `{"error": {"code","message","details?"}}` shape and that `code` values are stable strings (FR-020, FR-021)
- [ ] T024 [P] Write failing unit tests in [tests/WebApp.Tests/Observability/CorrelationIdTests.cs](../../tests/WebApp.Tests/Observability/CorrelationIdTests.cs) asserting that an inbound `X-Correlation-Id` is honored, a missing one is generated as a ULID, and the value is echoed on the response (FR-041, FR-042)

### Shared implementation (after T021–T024 are RED)

- [ ] T025 [P] Implement Serilog redaction enricher in [src/WebApp/Observability/RedactionEnricher.cs](../../src/WebApp/Observability/RedactionEnricher.cs) per [research.md](./research.md) §7; wire into Serilog config in `src/WebApp/Program.cs` — T021 turns GREEN
- [ ] T026 [P] Implement the same redaction enricher in [src/McpServer/Observability/RedactionEnricher.cs](../../src/McpServer/Observability/RedactionEnricher.cs) (identical policy); wire into Serilog config in `src/McpServer/Program.cs` — T022 turns GREEN
- [ ] T027 [P] Implement `ErrorEnvelope` and `ErrorCode` constants in [src/WebApp/Api/ErrorEnvelope.cs](../../src/WebApp/Api/ErrorEnvelope.cs) — T023 turns GREEN
- [ ] T028 Implement correlation-id middleware in [src/WebApp/Observability/CorrelationIdMiddleware.cs](../../src/WebApp/Observability/CorrelationIdMiddleware.cs) (read `X-Correlation-Id`, generate ULID if absent, push to Serilog `LogContext` + OTel span attribute `correlation.id`, echo on response) — T024 turns GREEN
- [ ] T029 [P] Implement OTel base wiring in [src/WebApp/Observability/Telemetry.cs](../../src/WebApp/Observability/Telemetry.cs) (ASP.NET Core + HttpClient + EF Core + OTLP exporters)
- [ ] T030 [P] Implement OTel base wiring in [src/McpServer/Observability/Telemetry.cs](../../src/McpServer/Observability/Telemetry.cs) (HttpClient + OTLP exporters)

### Shared Azure infra (Bicep modules used by both services)

- [ ] T031 [P] Create [infra/modules/uami.bicep](../../infra/modules/uami.bicep) — UAMI `uami-taskmgr` with `FederatedIdentityCredentials` binding to `system:serviceaccount:taskmgr:taskmgr-sa` (AKS OIDC issuer)
- [ ] T032 [P] Create [infra/modules/acr.bicep](../../infra/modules/acr.bicep) — ACR Basic + `AcrPull` role assignment to the UAMI
- [ ] T033 [P] Create [infra/modules/loganalytics.bicep](../../infra/modules/loganalytics.bicep) — Log Analytics workspace + Application Insights
- [ ] T034 [P] Create [infra/modules/aks.bicep](../../infra/modules/aks.bicep) — created only if `infra/aks.discovered.json` indicates no existing AKS (per `scripts/aks-discover.sh`)
- [ ] T035 [infra/main.bicep](../../infra/main.bicep) wires modules from T031–T034; reads RG = `sainitesh-test`; subscription pinned via deployment-time check
- [ ] T036 [P] Create [infra/params/dev.bicepparam](../../infra/params/dev.bicepparam) and [infra/params/prod.bicepparam](../../infra/params/prod.bicepparam) with environment-specific parameter values

### Shared Kustomize base (namespace + service account)

- [ ] T037 [P] Create [deploy/base/namespace.yaml](../../deploy/base/namespace.yaml) (`taskmgr` namespace)
- [ ] T038 Create [deploy/base/serviceaccount.yaml](../../deploy/base/serviceaccount.yaml) — `taskmgr-sa` annotated `azure.workload.identity/client-id: <uami-clientId>` (templated for Kustomize replacement)
- [ ] T039 [P] Create [deploy/base/kustomization.yaml](../../deploy/base/kustomization.yaml) referencing namespace + serviceaccount (other resources added by story phases)
- [ ] T040 [P] Create [deploy/overlays/dev/kustomization.yaml](../../deploy/overlays/dev/kustomization.yaml) and [deploy/overlays/prod/kustomization.yaml](../../deploy/overlays/prod/kustomization.yaml) (empty patches; story-specific patches added later)

**Checkpoint**: Foundation ready — both projects compile, redaction enricher and correlation-id middleware are GREEN, shared infra modules exist, namespace + service account ready. Story phases may now begin in parallel.

---

## Phase 3: User Story 1 — Manage Tasks via REST API (Priority: P1) 🎯 MVP

**Goal**: Deliver the versioned `/api/v1/tasks` REST surface (full CRUD + filtering + pagination + uniform error envelope) backed by EF Core 10 + PostgreSQL, deployable to AKS as the Web App image.

**Independent Test**: Issue `curl`/Postman calls against `http://webapp.taskmgr.svc.cluster.local/api/v1/tasks` in-cluster (or via `kubectl port-forward`) and exercise all 8 acceptance scenarios in [spec.md](./spec.md) User Story 1 without the MCP server being deployed.

### Failing tests for User Story 1 (write FIRST — Principle III) ⚠️

- [ ] T041 [P] [US1] Contract test in [tests/WebApp.Tests/Api/CreateTaskContractTests.cs](../../tests/WebApp.Tests/Api/CreateTaskContractTests.cs) — `POST /api/v1/tasks` returns 201 with full task body, defaults `status=todo` `priority=medium`, populates timestamps (acceptance scenario 1, FR-010, FR-002)
- [ ] T042 [P] [US1] Contract test in [tests/WebApp.Tests/Api/GetTaskContractTests.cs](../../tests/WebApp.Tests/Api/GetTaskContractTests.cs) — `GET /api/v1/tasks/{id}` returns 200/404 (scenarios 2, 8, FR-012)
- [ ] T043 [P] [US1] Contract test in [tests/WebApp.Tests/Api/ListTasksContractTests.cs](../../tests/WebApp.Tests/Api/ListTasksContractTests.cs) — filters `status`/`priority`/`due_before`/`due_after`, pagination `page`/`page_size`, response includes `items`+`page`+`page_size`+`total` (scenario 3, FR-011)
- [ ] T044 [P] [US1] Contract test in [tests/WebApp.Tests/Api/PutTaskContractTests.cs](../../tests/WebApp.Tests/Api/PutTaskContractTests.cs) — `PUT /api/v1/tasks/{id}` full replacement, missing required field → 400 (scenario 4, FR-013, edge case)
- [ ] T045 [P] [US1] Contract test in [tests/WebApp.Tests/Api/PatchTaskContractTests.cs](../../tests/WebApp.Tests/Api/PatchTaskContractTests.cs) — `PATCH /api/v1/tasks/{id}` partial; status-only and priority-only patches; empty body → 400 (scenario 5, FR-014)
- [ ] T046 [P] [US1] Contract test in [tests/WebApp.Tests/Api/DeleteTaskContractTests.cs](../../tests/WebApp.Tests/Api/DeleteTaskContractTests.cs) — `DELETE` returns 204; subsequent GET returns 404; second DELETE returns 404 (scenario 6, FR-015, edge case)
- [ ] T047 [P] [US1] Validation test in [tests/WebApp.Tests/Validation/TaskValidatorTests.cs](../../tests/WebApp.Tests/Validation/TaskValidatorTests.cs) covering ALL [data-model.md](./data-model.md) validation rules: title 0/1/200/201, description 2000/2001, invalid enums, malformed `due_date`, pagination bounds, filter window (FR-003 through FR-006, SC-008)
- [ ] T048 [P] [US1] Integration test in [tests/WebApp.Tests/Integration/PostgresPersistenceTests.cs](../../tests/WebApp.Tests/Integration/PostgresPersistenceTests.cs) using `Testcontainers.PostgreSql` — verifies initial migration applies, CHECK constraints reject invalid rows, indexes exist
- [ ] T049 [P] [US1] Health-check test in [tests/WebApp.Tests/HealthChecks/HealthEndpointTests.cs](../../tests/WebApp.Tests/HealthChecks/HealthEndpointTests.cs) — `/healthz` returns 200 always; `/readyz` returns 200 only when DB reachable, 503 otherwise

### Domain + persistence implementation (after tests above are RED)

- [ ] T050 [P] [US1] Define `TaskItem` entity, `TaskStatus`, `TaskPriority` enums in [src/WebApp/Domain/TaskItem.cs](../../src/WebApp/Domain/TaskItem.cs) per [data-model.md](./data-model.md)
- [ ] T051 [P] [US1] Implement `TaskListPage<T>` DTO in [src/WebApp/Domain/TaskListPage.cs](../../src/WebApp/Domain/TaskListPage.cs)
- [ ] T052 [US1] Implement `TaskDbContext` and EF Core configuration in [src/WebApp/Persistence/TaskDbContext.cs](../../src/WebApp/Persistence/TaskDbContext.cs) (enum value converters, indexes `ix_tasks_status_priority_due_date` and `ix_tasks_created_at_desc`, `Id` `ValueGeneratedNever()`); depends on T050
- [ ] T053 [US1] Generate initial migration `0001_initial` in [src/WebApp/Persistence/Migrations/](../../src/WebApp/Persistence/Migrations/) producing the schema in [data-model.md](./data-model.md) (including `pgcrypto` extension and CHECK constraints); depends on T052
- [ ] T054 [P] [US1] Implement FluentValidation validators in [src/WebApp/Validation/](../../src/WebApp/Validation/) (`CreateTaskValidator.cs`, `UpdateTaskValidator.cs`, `PatchTaskValidator.cs`, `ListTasksQueryValidator.cs`) — T047 turns GREEN
- [ ] T055 [US1] Implement repository / service layer in [src/WebApp/Persistence/TaskRepository.cs](../../src/WebApp/Persistence/TaskRepository.cs) with create/get/list (with filters+pagination)/replace/patch/delete

### API endpoint implementation

- [ ] T056 [US1] Implement endpoint group `/api/v1/tasks` in [src/WebApp/Api/TasksEndpoints.cs](../../src/WebApp/Api/TasksEndpoints.cs) wiring POST/GET-list/GET-by-id/PUT/PATCH/DELETE; map validation failures to `validation_error`, NotFound to `not_found` per FR-020/FR-021 — T041–T046 turn GREEN
- [ ] T057 [US1] Configure JSON options in `src/WebApp/Program.cs` to ignore unknown request fields (FR-023) and emit lower-snake-case enum values matching [data-model.md](./data-model.md)
- [ ] T058 [US1] Wire Swashbuckle to publish OpenAPI 3.1 at `/openapi/v1.json` matching [contracts/webapp-openapi.yaml](./contracts/webapp-openapi.yaml); add contract-drift test in [tests/WebApp.Tests/Api/OpenApiDriftTest.cs](../../tests/WebApp.Tests/Api/OpenApiDriftTest.cs) that diffs the published spec against the checked-in YAML
- [ ] T059 [US1] Wire Entra ID token-based Postgres authentication via `DefaultAzureCredential` in `src/WebApp/Program.cs` (no static password — Workload Identity)

### Health checks (US1 portion)

- [ ] T060 [P] [US1] Implement `/healthz` (liveness, always 200) in [src/WebApp/HealthChecks/HealthEndpoints.cs](../../src/WebApp/HealthChecks/HealthEndpoints.cs) — T049 partly GREEN
- [ ] T061 [US1] Implement `/readyz` (readiness — checks DB reachability via a cheap `SELECT 1`) in `src/WebApp/HealthChecks/HealthEndpoints.cs` — T049 fully GREEN

### Container

- [ ] T062 [US1] Create [Dockerfile.webapp](../../Dockerfile.webapp) — multi-stage build, `ARG BASE_IMAGE` sourced from `infra/base-images.lock`, runs as uid `10001`, no shell in final layer

### Kustomize (webapp manifests, in `deploy/base/`)

- [ ] T063 [P] [US1] Create [deploy/base/webapp-deployment.yaml](../../deploy/base/webapp-deployment.yaml) with the constitution's pod `securityContext` (non-root 10001, `readOnlyRootFilesystem`, `allowPrivilegeEscalation=false`, `capabilities.drop=["ALL"]`, `seccompProfile=RuntimeDefault`, writable `emptyDir` at `/tmp` only), `azure.workload.identity/use: "true"`, liveness=`/healthz`, readiness=`/readyz`, 2 replicas
- [ ] T064 [P] [US1] Create [deploy/base/webapp-service.yaml](../../deploy/base/webapp-service.yaml) — `ClusterIP` on port 80 → container 8080
- [ ] T065 [US1] Create [deploy/base/webapp-migrate-job.yaml](../../deploy/base/webapp-migrate-job.yaml) — Kubernetes `Job` `webapp-migrate` that runs `dotnet WebApp.dll migrate` (or `efbundle`) against Postgres BEFORE the Web App `Deployment` rolls out; uses the same UAMI/SA; restartPolicy=`OnFailure`; ordered via Argo/Kustomize annotation or wave hook so it completes before the Deployment becomes ready
- [ ] T066 [US1] Update [deploy/base/kustomization.yaml](../../deploy/base/kustomization.yaml) to include `webapp-deployment.yaml`, `webapp-service.yaml`, `webapp-migrate-job.yaml`

### Bicep (Postgres for US1)

- [ ] T067 [US1] Create [infra/modules/postgres.bicep](../../infra/modules/postgres.bicep) — Azure Database for PostgreSQL Flexible Server, **Burstable B1ms**, Entra ID admin only (no admin password), `pgcrypto` extension allowed; outputs FQDN
- [ ] T068 [US1] Wire `postgres.bicep` into [infra/main.bicep](../../infra/main.bicep); add Postgres FQDN as a Kustomize ConfigMap value or env var on the webapp Deployment

### CI wiring for US1

- [ ] T069 [US1] In [.github/workflows/ci.yml](../../.github/workflows/ci.yml), wire `build` → `dotnet build TaskManager.sln`; `unit-tests` → `dotnet test tests/WebApp.Tests` with coverage; `integration-tests` → `dotnet test --filter Category=Integration` (Testcontainers); enforce **≥ 80 %** line coverage gate
- [ ] T070 [US1] In ci.yml `push` job, build `Dockerfile.webapp`, tag with git SHA, push to ACR via Workload Identity (`az acr login` + `docker push`) — depends on prior jobs

**Checkpoint**: User Story 1 fully functional. The Web App can be `kubectl apply -k deploy/overlays/dev`-ed, the migrate Job runs first, and all 8 acceptance scenarios pass against the deployed `/api/v1/tasks` surface. MVP shippable.

---

## Phase 4: User Story 2 — Manage Tasks via AI Agent Through MCP (Priority: P2)

**Goal**: Deliver the MCP server exposing exactly six tools (`create_task`, `list_tasks`, `get_task`, `update_task_status`, `update_task_priority`, `delete_task`) that call the Web App through a typed `HttpClient` + Polly resilience, with correlation-id propagation and the `MCP_ALLOW_MUTATIONS` gate.

**Independent Test**: Connect any MCP client (MCP inspector or in-memory transport test) to the deployed MCP server `Service`; invoke each tool with `MCP_ALLOW_MUTATIONS=true`; verify each call produces the documented backing REST call carrying `X-Correlation-Id` and returns the documented JSON shape from [contracts/mcp-tools.md](./contracts/mcp-tools.md). All 6 acceptance scenarios in spec User Story 2 pass without any AI agent in the loop.

### Failing tests for User Story 2 (write FIRST — Principle III) ⚠️

- [ ] T071 [P] [US2] Protocol-pinning test in [tests/McpServer.Tests/Protocol/PinnedVersionTests.cs](../../tests/McpServer.Tests/Protocol/PinnedVersionTests.cs) — asserts the SDK's advertised `ProtocolVersion` in the `initialize` response equals `2025-06-18` (Principle I)
- [ ] T072 [P] [US2] Pipeline test in [tests/McpServer.Tests/Pipeline/CorrelationIdHandlerTests.cs](../../tests/McpServer.Tests/Pipeline/CorrelationIdHandlerTests.cs) — inbound `_meta.correlationId` is forwarded as `X-Correlation-Id` on the backing call; absent inbound → ULID generated and echoed in tool response (FR-041, FR-042)
- [ ] T073 [P] [US2] Pipeline test in [tests/McpServer.Tests/Pipeline/ResiliencePipelineTests.cs](../../tests/McpServer.Tests/Pipeline/ResiliencePipelineTests.cs) — Polly v8 timeout + retry + circuit breaker stays within the 5 s budget (SC-009); unreachable upstream returns `upstream_unavailable` envelope (FR-044)
- [ ] T074 [P] [US2] Mutation-gate test in [tests/McpServer.Tests/Mutation/MutationGateTests.cs](../../tests/McpServer.Tests/Mutation/MutationGateTests.cs) — when `MCP_ALLOW_MUTATIONS` is unset/empty/`false`/any non-`true` value, every mutation tool returns the `mutations_disabled` envelope from [contracts/mcp-tools.md](./contracts/mcp-tools.md#mutation-gate-principle-ii) **without issuing any HTTP call** (verified by WireMock recording zero calls)
- [ ] T075 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/CreateTaskToolTests.cs](../../tests/McpServer.Tests/Tools/CreateTaskToolTests.cs) using WireMock.Net (acceptance scenario 1)
- [ ] T076 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/ListTasksToolTests.cs](../../tests/McpServer.Tests/Tools/ListTasksToolTests.cs) — filters + pagination forwarded, page_size capped at 100 (scenario 2, FR-032)
- [ ] T077 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/GetTaskToolTests.cs](../../tests/McpServer.Tests/Tools/GetTaskToolTests.cs) — unknown id → structured `not_found` error, no exception leaked (scenario 3, FR-043)
- [ ] T078 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/UpdateTaskStatusToolTests.cs](../../tests/McpServer.Tests/Tools/UpdateTaskStatusToolTests.cs) — only `status` patched, other fields untouched (scenario 4, FR-033)
- [ ] T079 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/UpdateTaskPriorityToolTests.cs](../../tests/McpServer.Tests/Tools/UpdateTaskPriorityToolTests.cs) — only `priority` patched (FR-033)
- [ ] T080 [P] [US2] Per-tool test in [tests/McpServer.Tests/Tools/DeleteTaskToolTests.cs](../../tests/McpServer.Tests/Tools/DeleteTaskToolTests.cs) — valid id → success; unknown id → `not_found`; no crash (scenario 6)
- [ ] T081 [P] [US2] Health-check test in [tests/McpServer.Tests/HealthChecks/HealthEndpointTests.cs](../../tests/McpServer.Tests/HealthChecks/HealthEndpointTests.cs) — `/healthz` always 200; `/readyz` 200 only when backing Web App reachable

### Backing client + resilience pipeline (after tests above are RED)

- [ ] T082 [P] [US2] Implement backing DTOs mirroring [contracts/webapp-openapi.yaml](./contracts/webapp-openapi.yaml) in [src/McpServer/Backing/TaskDtos.cs](../../src/McpServer/Backing/TaskDtos.cs)
- [ ] T083 [US2] Implement `ITaskApiClient` + typed `HttpClient` `TaskApiClient` in [src/McpServer/Backing/TaskApiClient.cs](../../src/McpServer/Backing/TaskApiClient.cs) covering all 6 backing endpoints
- [ ] T084 [US2] Implement `CorrelationIdHandler` `DelegatingHandler` in [src/McpServer/Pipeline/CorrelationIdHandler.cs](../../src/McpServer/Pipeline/CorrelationIdHandler.cs) — reads ambient correlation id (LogContext / AsyncLocal) and sets `X-Correlation-Id` — T072 turns GREEN
- [ ] T085 [US2] Configure `Microsoft.Extensions.Http.Resilience` pipeline (Polly v8: total-request-timeout, retry with jitter, circuit breaker) on the typed client in `src/McpServer/Program.cs` per [research.md](./research.md) §2; bounded ≤ 5 s — T073 turns GREEN

### Mutation gate + tool registrations

- [ ] T086 [US2] Implement `MutationGate` reading `MCP_ALLOW_MUTATIONS` (case-insensitive `true`) in [src/McpServer/Mutation/MutationGate.cs](../../src/McpServer/Mutation/MutationGate.cs); implement `MutationsDisabledResult` matching the [contracts/mcp-tools.md](./contracts/mcp-tools.md#structured-mutations_disabled-response) envelope — T074 turns GREEN
- [ ] T087 [US2] Implement `ErrorTranslator` in [src/McpServer/Tools/ErrorTranslator.cs](../../src/McpServer/Tools/ErrorTranslator.cs) mapping backing `ErrorEnvelope` → MCP `isError:true` envelope (FR-043) and circuit/timeout failures → `upstream_unavailable` (FR-044)
- [ ] T088 [P] [US2] Implement `CreateTaskTool` in [src/McpServer/Tools/CreateTaskTool.cs](../../src/McpServer/Tools/CreateTaskTool.cs) — gated by `MutationGate` — T075 turns GREEN
- [ ] T089 [P] [US2] Implement `ListTasksTool` in [src/McpServer/Tools/ListTasksTool.cs](../../src/McpServer/Tools/ListTasksTool.cs) — T076 turns GREEN
- [ ] T090 [P] [US2] Implement `GetTaskTool` in [src/McpServer/Tools/GetTaskTool.cs](../../src/McpServer/Tools/GetTaskTool.cs) — T077 turns GREEN
- [ ] T091 [P] [US2] Implement `UpdateTaskStatusTool` in [src/McpServer/Tools/UpdateTaskStatusTool.cs](../../src/McpServer/Tools/UpdateTaskStatusTool.cs) — gated; T078 turns GREEN
- [ ] T092 [P] [US2] Implement `UpdateTaskPriorityTool` in [src/McpServer/Tools/UpdateTaskPriorityTool.cs](../../src/McpServer/Tools/UpdateTaskPriorityTool.cs) — gated; T079 turns GREEN
- [ ] T093 [P] [US2] Implement `DeleteTaskTool` in [src/McpServer/Tools/DeleteTaskTool.cs](../../src/McpServer/Tools/DeleteTaskTool.cs) — gated; T080 turns GREEN
- [ ] T094 [US2] Register all 6 tools with the MCP host using `ModelContextProtocol` SDK `.WithHttpTransport()` in `src/McpServer/Program.cs`; pin MCP spec `2025-06-18` — T071 turns GREEN

### Health checks (US2 portion)

- [ ] T095 [US2] Implement `/healthz` and `/readyz` (readyz pings Web App `/healthz`) in [src/McpServer/HealthChecks/HealthEndpoints.cs](../../src/McpServer/HealthChecks/HealthEndpoints.cs) — T081 turns GREEN

### Container

- [ ] T096 [US2] Create [Dockerfile.mcpserver](../../Dockerfile.mcpserver) — multi-stage, same pinned base image, uid `10001`, no shell in final layer

### Kustomize (mcp manifests)

- [ ] T097 [P] [US2] Create [deploy/base/mcp-server-deployment.yaml](../../deploy/base/mcp-server-deployment.yaml) with the same hardened `securityContext` as the webapp, `azure.workload.identity/use: "true"`, env `BACKING_API_BASE_URL=http://webapp.taskmgr.svc.cluster.local`, liveness/readiness probes, 1–2 replicas, pod label `app: mcp-server`; **does NOT** set `MCP_ALLOW_MUTATIONS` in base
- [ ] T098 [P] [US2] Create [deploy/base/mcp-server-service.yaml](../../deploy/base/mcp-server-service.yaml) — `ClusterIP`, selector `app: mcp-server`
- [ ] T099 [US2] Update [deploy/base/kustomization.yaml](../../deploy/base/kustomization.yaml) to include the two MCP manifests
- [ ] T100 [US2] Create [deploy/overlays/dev/mcp-allow-mutations-patch.yaml](../../deploy/overlays/dev/mcp-allow-mutations-patch.yaml) setting `MCP_ALLOW_MUTATIONS=true` on the MCP Deployment; wire into `deploy/overlays/dev/kustomization.yaml` (Principle II dev exception)

### CI wiring for US2

- [ ] T101 [US2] Extend ci.yml `unit-tests` to also run `dotnet test tests/McpServer.Tests` with coverage; aggregate coverage gate ≥ 80 %
- [ ] T102 [US2] Extend ci.yml `push` job to build `Dockerfile.mcpserver`, tag with git SHA, push to ACR

**Checkpoint**: User Stories 1 AND 2 are independently functional. AI agent (or MCP inspector) can drive end-to-end task lifecycle through the MCP server in the dev overlay; correlation ids reconcile in webapp logs (SC-006).

---

## Phase 5: User Story 3 — Versioned API Surface Ready for Future Auth (Priority: P3)

**Goal**: Mechanically guarantee the v1 contract is auth-additive and forward-compatible: every path under `/api/v1/`, uniform error envelope, unknown fields ignored, no endpoint depends on the absence of `Authorization`. Topology authorization (default-deny + MCP→Web App-only egress) and supply-chain gates (Trivy, prod-overlay lint) enforce this guarantee at deploy and CI time.

**Independent Test**: (1) Re-run the US1 contract test suite with an `Authorization: Bearer dummy` header injected on every request — all tests still pass (SC-007 dry-run). (2) Apply `deploy/overlays/prod` to a kind/dev cluster; confirm `kubectl get netpol -n taskmgr` shows default-deny + the two allow policies and that an `nc` from any other namespace to the webapp Service fails. (3) Confirm CI fails when `MCP_ALLOW_MUTATIONS=true` is staged into the prod overlay.

### Failing tests for User Story 3 (write FIRST — Principle III) ⚠️

- [ ] T103 [P] [US3] Contract test in [tests/WebApp.Tests/Api/V1PrefixTests.cs](../../tests/WebApp.Tests/Api/V1PrefixTests.cs) — enumerates every registered route and asserts each begins with `/api/v1/` except `/healthz`, `/readyz`, `/openapi/*` (FR-022)
- [ ] T104 [P] [US3] Contract test in [tests/WebApp.Tests/Api/ErrorEnvelopeUniformityTests.cs](../../tests/WebApp.Tests/Api/ErrorEnvelopeUniformityTests.cs) — exercises 400 / 404 / 405 / 409 / 500 paths and asserts every body matches `{"error":{"code","message","details?"}}` (FR-020, SC-002)
- [ ] T105 [P] [US3] Contract test in [tests/WebApp.Tests/Api/UnknownFieldsIgnoredTests.cs](../../tests/WebApp.Tests/Api/UnknownFieldsIgnoredTests.cs) — POST/PUT/PATCH bodies with extra unknown fields succeed (FR-023, scenario 3)
- [ ] T106 [P] [US3] Auth-additive test in [tests/WebApp.Tests/Api/AuthAdditiveTests.cs](../../tests/WebApp.Tests/Api/AuthAdditiveTests.cs) — every endpoint succeeds both with and without an `Authorization: Bearer dummy` header in v1 (FR-024, SC-007)
- [ ] T107 [P] [US3] Script test in [tests/Scripts/LintProdOverlayTests.sh](../../tests/Scripts/LintProdOverlayTests.sh) — calls `scripts/lint-prod-overlay.sh` against a temp directory containing `MCP_ALLOW_MUTATIONS=true` and asserts non-zero exit; with the real `deploy/overlays/prod/` it must exit zero

### Implementation for User Story 3

- [ ] T108 [US3] Ensure the route-discovery and JSON-options changes from T057/T058 cover T103, T104, T105; add `Microsoft.AspNetCore.Authentication` no-op pass-through (or assertion comment) so T106 documents the auth-additive guarantee
- [ ] T109 [US3] Confirm `scripts/lint-prod-overlay.sh` from T007 satisfies T107; refine grep patterns if needed
- [ ] T110 [P] [US3] Create [deploy/base/networkpolicy-default-deny.yaml](../../deploy/base/networkpolicy-default-deny.yaml) — denies all ingress AND egress in the `taskmgr` namespace
- [ ] T111 [P] [US3] Create [deploy/base/networkpolicy-webapp-ingress.yaml](../../deploy/base/networkpolicy-webapp-ingress.yaml) — allows ingress to the webapp Service from pods labelled `app=mcp-server` only (and from kube-system DNS)
- [ ] T112 [P] [US3] Create [deploy/base/networkpolicy-mcp-egress.yaml](../../deploy/base/networkpolicy-mcp-egress.yaml) — allows egress from pods labelled `app=mcp-server` ONLY to the webapp `ClusterIP` Service (port 80) + DNS (kube-system :53) + the OTel collector; everything else denied
- [ ] T113 [US3] Update [deploy/base/kustomization.yaml](../../deploy/base/kustomization.yaml) to include the three NetworkPolicy manifests

### CI wiring for User Story 3

- [ ] T114 [US3] In [.github/workflows/ci.yml](../../.github/workflows/ci.yml), add a `lint-prod-overlay` job that runs `scripts/lint-prod-overlay.sh deploy/overlays/prod`; this job must succeed before `deploy` runs
- [ ] T115 [US3] In ci.yml, wire `trivy-scan` job to run `aquasec/trivy-action` against both `Dockerfile.webapp` and `Dockerfile.mcpserver` built images; **fail the build on any HIGH or CRITICAL CVE**; this job must succeed before `push`
- [ ] T116 [US3] In ci.yml `deploy` job, use GitHub OIDC `azure/login@v2` (no client secret) to assume the UAMI; run `kubectl apply -k deploy/overlays/<env>` after ensuring `webapp-migrate` Job completes successfully before the webapp Deployment rolls out (wave annotation or explicit `kubectl wait --for=condition=complete job/webapp-migrate`)
- [ ] T116a [US3] Create [infra/modules/alerts.bicep](../../infra/modules/alerts.bicep) provisioning two Azure Monitor alert rules required by the Constitution before prod cutover: (1) a metric alert on Application Insights `requests/failed` filtered to `name == '/readyz' && resultCode startswith '5'` over 5 min with threshold `> 0`; (2) a log-search alert on `customMetrics | where name == 'redaction_failures_total' and value > 0` over 5 min. Both attach to the **existing** Action Group `Application Insights Smart Detection` in `sainitesh-test` (reused via `existing` Bicep reference, no new action group created). Wire the module into `infra/main.bicep`. A failing test in `tests/Infra.Tests` (or a `bicep build` + `jq` assertion in CI) confirms both alert rules are present in the deployed template.

**Checkpoint**: All three user stories independently functional. The v1 contract is mechanically guaranteed auth-additive, the network topology enforces MCP→Web App-only egress, Trivy gates the supply chain, and CI prevents `MCP_ALLOW_MUTATIONS` from ever reaching prod.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, docs, performance check, repo hygiene.

- [ ] T117 [P] Run the [quickstart.md](./quickstart.md) end-to-end against a fresh `kind` or dev AKS cluster; capture any drift and file follow-ups
- [ ] T118 [P] Performance test: under 50 concurrent clients with a 10 000-row dataset, verify `GET /api/v1/tasks` p95 < 300 ms (SC-003) and `POST /api/v1/tasks` p95 < 200 ms (SC-004) using `k6` or `bombardier`
- [ ] T119 [P] SC-005 dry-run: drive all 6 MCP tools through an MCP inspector session and confirm a full task lifecycle without any direct HTTP/DB calls
- [ ] T120 [P] Verify aggregate line coverage ≥ 80 % across `tests/WebApp.Tests` + `tests/McpServer.Tests`; fail-fast in CI on regression
- [ ] T121 Audit logs from a sample run with `kubectl logs` and assert that no secret / token / connection string ever appears (Principle V acceptance gate)
- [ ] T122 Update [README.md](../../README.md) at repo root with one-paragraph overview, link to [quickstart.md](./quickstart.md), and link to constitution
- [ ] T123 Tag the release commit `v1.0.0` once all checkpoints are green

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup; **BLOCKS** all user-story phases
- **US1 (Phase 3)**: depends only on Foundational
- **US2 (Phase 4)**: depends only on Foundational. Integration tests use WireMock so US2 does NOT depend on US1 source; for live in-cluster validation US1 must be deployed
- **US3 (Phase 5)**: depends on Foundational. T103–T106 reuse `WebApplicationFactory` from US1 scaffolding, so in practice run after US1 endpoint code exists. NetworkPolicies (T110–T113) can be authored anytime after T037–T039
- **Polish (Phase 6)**: depends on all desired user stories

### Within Each User Story

- All `Tests for User Story N` tasks MUST be RED (failing) before any implementation task in that story begins (Principle III, NON-NEGOTIABLE)
- Domain/models before persistence; persistence before services; services before endpoints/tools
- Endpoints/tools before container; container before Kustomize; Kustomize before Bicep wiring; everything before CI wiring

### Parallel Opportunities

- Phase 1 T002–T011 all `[P]`
- Phase 2 scaffolding T013–T020 all `[P]`; failing-tests T021–T024 all `[P]`; shared implementations T025–T030 all `[P]` (different files); infra modules T031–T034 all `[P]`
- Within US1: tests T041–T049 all `[P]`; entity/DTO T050–T051 `[P]`; validators T054 `[P]`; kustomize manifests T063–T064 `[P]`
- Within US2: tests T071–T081 all `[P]`; per-tool implementations T088–T093 all `[P]` (different files); kustomize manifests T097–T098 `[P]`
- Within US3: tests T103–T107 all `[P]`; NetworkPolicies T110–T112 all `[P]`
- Across stories: once Foundational is green, US1, US2, US3 can be staffed in parallel

---

## Parallel Example: Failing tests at the start of User Story 1

```text
# Launch all US1 contract + validation + integration + health tests in parallel:
T041 [P] [US1] tests/WebApp.Tests/Api/CreateTaskContractTests.cs
T042 [P] [US1] tests/WebApp.Tests/Api/GetTaskContractTests.cs
T043 [P] [US1] tests/WebApp.Tests/Api/ListTasksContractTests.cs
T044 [P] [US1] tests/WebApp.Tests/Api/PutTaskContractTests.cs
T045 [P] [US1] tests/WebApp.Tests/Api/PatchTaskContractTests.cs
T046 [P] [US1] tests/WebApp.Tests/Api/DeleteTaskContractTests.cs
T047 [P] [US1] tests/WebApp.Tests/Validation/TaskValidatorTests.cs
T048 [P] [US1] tests/WebApp.Tests/Integration/PostgresPersistenceTests.cs
T049 [P] [US1] tests/WebApp.Tests/HealthChecks/HealthEndpointTests.cs
```

## Parallel Example: Per-tool implementations in User Story 2

```text
# Once T082–T087 are in place, the 6 tool implementations are independent files:
T088 [P] [US2] src/McpServer/Tools/CreateTaskTool.cs
T089 [P] [US2] src/McpServer/Tools/ListTasksTool.cs
T090 [P] [US2] src/McpServer/Tools/GetTaskTool.cs
T091 [P] [US2] src/McpServer/Tools/UpdateTaskStatusTool.cs
T092 [P] [US2] src/McpServer/Tools/UpdateTaskPriorityTool.cs
T093 [P] [US2] src/McpServer/Tools/DeleteTaskTool.cs
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup
2. Phase 2 Foundational (CRITICAL — blocks all stories)
3. Phase 3 US1 end-to-end (REST API + migrate Job + webapp deploy + Postgres)
4. **STOP and VALIDATE**: run all 8 US1 acceptance scenarios, SC-001, SC-003, SC-004 against the deployed Web App
5. Tag and demo MVP

### Incremental delivery

1. Setup + Foundational → foundation ready
2. + US1 → REST MVP shippable
3. + US2 → MCP surface shippable (dev overlay only, mutations enabled)
4. + US3 → prod-ready (NetworkPolicy + Trivy gate + prod-overlay lint enforced)
5. + Polish → release `v1.0.0`

### Parallel team strategy

After Phase 2 is complete:

- Developer A: US1 (REST API + Postgres + migrate Job)
- Developer B: US2 (MCP server + Polly + mutation gate)
- Developer C: US3 (NetworkPolicies + Trivy + prod-overlay lint + auth-additive tests)

Each story is independently testable and integrates only at deploy time.

---

## Notes

- `[P]` = different files, no ordering dependency on incomplete tasks
- `[Story]` labels enable per-story traceability and independent rollback
- Principle III is NON-NEGOTIABLE: every `Tests for User Story N` block MUST be observed RED before the implementation tasks in that block are started
- Principle V is enforced by T021/T022 (failing-first redaction tests) before the enrichers exist
- Principle II's documented dev-overlay exception is materially enforced by T074 (gate test) + T100 (dev patch) + T107/T109/T114 (prod-overlay lint) + T115 (Trivy)
- The Kubernetes `Job` `webapp-migrate` (T065) applies EF Core migrations BEFORE the webapp Deployment rolls out, satisfying the plan's "schema owned by the Web App via EF Core migrations applied by a Kubernetes Job"
- All Azure operations are gated by `scripts/assert-azure-context.sh` (T006 + T012) — wrong subscription / wrong RG fails fast before any `az` call
- Commit per task or per parallel batch; do not let RED tests linger across PR boundaries
