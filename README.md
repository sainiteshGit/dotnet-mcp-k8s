# dotnet-mcp-k8s

A spec-driven .NET 10 demo: a **Task Manager REST API** (WebApp) and a
**Model Context Protocol** server (McpServer) that exposes those REST
endpoints as MCP tools — both packaged as container images, deployable
to Azure Kubernetes Service via Bicep + Kustomize, runnable locally via
`docker-compose`.

> **Status**: Phases 1–5 complete (167/167 tests green). End-to-end
> verified locally; Azure deploy gated on subscription quota.

---

## Architecture

```
┌──────────────────┐   tools/call    ┌──────────────────┐   HTTPS    ┌──────────────────┐
│  MCP client      │ ─────────────▶  │  McpServer       │ ─────────▶ │  WebApp          │
│  (Copilot Chat,  │   JSON-RPC      │  (Streamable     │   REST     │  /api/v1/tasks   │
│   Claude, etc.)  │ ◀─────────────  │   HTTP, :8081)   │ ◀───────── │  (.NET 10, :8080)│
└──────────────────┘                 └──────────────────┘            └────────┬─────────┘
                                                                              │ EF Core
                                                                              ▼
                                                                     ┌──────────────────┐
                                                                     │  PostgreSQL 16   │
                                                                     └──────────────────┘
```

- **WebApp** — Minimal API + EF Core (Npgsql), Serilog JSON + secret
  redaction, OpenTelemetry, Scalar UI (dev), `/healthz` + `/readyz`.
- **McpServer** — Hosts the official `ModelContextProtocol.AspNetCore`
  package (protocol `2025-06-18`), 6 tools (`list_tasks`, `get_task`,
  `create_task`, `update_task_status`, `update_task_priority`,
  `delete_task`). Mutations are gated by `MCP_ALLOW_MUTATIONS`.
- **Containers** — Both images are .NET 10 Alpine, base pinned by sha256
  digest, run as non-root uid 10001 with `readOnlyRootFilesystem`.
- **Cluster** — AKS with Workload Identity (UAMI federated to a
  `ServiceAccount`); zero stored secrets. Kustomize `base` +
  `overlays/{dev,prod}`.
- **CI** — GitHub OIDC → UAMI; Trivy `HIGH,CRITICAL` scans block push;
  context guard (`AZURE_SUBSCRIPTION_ID` + `AZURE_RESOURCE_GROUP`) is the
  first job and gates every downstream stage.

---

## Run locally (no Azure needed)

```bash
docker compose -f docker-compose.local.yml up -d --build
# Postgres on :5432, WebApp on :8080, McpServer on :8081
```

Smoke-test the WebApp:

```bash
curl http://localhost:8080/readyz
curl -X POST http://localhost:8080/api/v1/tasks \
  -H 'content-type: application/json' \
  -d '{"title":"hello","priority":"low"}'
curl http://localhost:8080/api/v1/tasks
```

Open the **Scalar UI** at <http://localhost:8080/scalar/v1>.

Tear down:

```bash
docker compose -f docker-compose.local.yml down -v
```

---

## Use the MCP server from VS Code Copilot

The repo ships a workspace MCP config at `.vscode/mcp.json` (gitignored
by default — copy from below or run the local stack first):

```jsonc
{
  "servers": {
    "task-manager-local": {
      "type": "http",
      "url": "http://localhost:8081/"
    }
  }
}
```

Then in VS Code:

1. Command Palette → **MCP: List Servers** → start `task-manager-local`.
2. Open Copilot Chat in **Agent mode**.
3. Ask things like *"list my tasks"*, *"create a high-priority task
   titled 'ship it'"*, *"mark task `<id>` as done"*.

Copilot will invoke the corresponding MCP tools, which call the WebApp,
which persists to Postgres.

---

## Deploy to Azure

Prerequisites: `az`, `kubectl`, `kustomize`, an Azure subscription, and
the following env vars exported (or configured as GitHub repo
variables/secrets for CI):

```bash
export AZURE_SUBSCRIPTION_ID=<your-subscription-guid>
export AZURE_RESOURCE_GROUP=<your-rg-name>
export AZURE_REGION=eastus
```

See [specs/001-task-manager-api/quickstart.md](specs/001-task-manager-api/quickstart.md)
for the full end-to-end walkthrough.

---

## Repository layout

| Path | What |
|---|---|
| [src/WebApp/](src/WebApp/) | REST API (.NET 10 Minimal API + EF Core) |
| [src/McpServer/](src/McpServer/) | MCP server (`ModelContextProtocol.AspNetCore`) |
| [tests/](tests/) | xUnit unit + integration tests (167 total) |
| [infra/](infra/) | Bicep modules (`acr`, `aks`, `uami`, `postgres`, `loganalytics`, `main`) + `params/` |
| [deploy/](deploy/) | Kustomize `base` + `overlays/{dev,prod}` + NetworkPolicies |
| [scripts/](scripts/) | CI guard scripts (`assert-azure-context.sh`, `assert-region-availability.sh`, `aks-discover.sh`) |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | OIDC-based CI: guard → build → test → trivy → push → deploy |
| [Dockerfile.webapp](Dockerfile.webapp) / [Dockerfile.mcpserver](Dockerfile.mcpserver) | Multi-arch (amd64 + arm64), digest-pinned base |
| [docker-compose.local.yml](docker-compose.local.yml) | Local dev stack |
| [specs/001-task-manager-api/](specs/001-task-manager-api/) | Spec-Kit artefacts (`spec.md`, `plan.md`, `tasks.md`, `quickstart.md`, …) |
| [.specify/memory/constitution.md](.specify/memory/constitution.md) | Project constitution (binding) |

---

## Constitution highlights

- **Principle I** — Spec-driven: all features start with a `spec.md` +
  `plan.md` + `tasks.md` triplet.
- **Principle II** — MCP mutations are off by default
  (`MCP_ALLOW_MUTATIONS=false`); dev overlay opts in.
- **Principle III** — Tests are the contract: contract / integration /
  unit tests run on every push, no skipping.
- **Principle IV** — Containers are non-root, read-only-root, digest-
  pinned, scanned by Trivy.
- **Principle V** — Zero secrets in logs (Serilog redaction
  enricher, regex-based).
- **Principle VI** — Azure targeting is enforced by a CI guard; no
  drift to other subscriptions / RGs.

See [`.specify/memory/constitution.md`](.specify/memory/constitution.md) for the
full text.
