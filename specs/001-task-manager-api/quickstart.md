# Quickstart

End-to-end walkthrough: local dev loop → tests → cluster deploy. Targets the
subscription / resource group supplied at deploy time via the
`AZURE_SUBSCRIPTION_ID` and `AZURE_RESOURCE_GROUP` environment variables
(set as CI repository variables; for local runs export them in your shell
or a `.env` file). They are intentionally not committed to the repo.

---

## Prerequisites

- .NET SDK 9.0.x
- Docker Desktop (or any OCI-compatible engine) — needed for Testcontainers and image builds
- Azure CLI 2.60+
- `kubectl` 1.30+
- `kustomize` 5.x (or `kubectl -k`)
- Logged into Azure: `az login` then `az account set --subscription "$AZURE_SUBSCRIPTION_ID"`

Sanity check (will be run by CI too):

```bash
scripts/assert-azure-context.sh
```

Fails non-zero if the active subscription or resource group is wrong.

---

## 1. Restore + build + test (local)

From the repo root:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --collect:"XPlat Code Coverage"
```

What runs:

- `tests/WebApp.Tests` — handler, validation, contract, and **integration tests against a real PostgreSQL container** spun up by Testcontainers.
- `tests/McpServer.Tests` — per-tool tests with WireMock.Net faking the Web App, plus MCP protocol tests via the SDK's in-memory transport.

Coverage report is written under each test project's `TestResults/`. CI fails if aggregate coverage drops below 80%.

---

## 2. Run the Web App locally against a throwaway Postgres

```bash
# In one terminal: start Postgres (one-shot)
docker run --rm -d --name taskmgr-pg \
  -e POSTGRES_PASSWORD=devonly -e POSTGRES_DB=taskmgr -p 5432:5432 \
  postgres:16-alpine

# In another terminal: apply migrations + run
export ConnectionStrings__Tasks="Host=localhost;Database=taskmgr;Username=postgres;Password=devonly"
dotnet ef database update --project src/WebApp
dotnet run --project src/WebApp
```

The Web App listens on `http://localhost:8080`. Try:

```bash
curl -i http://localhost:8080/healthz
curl -i http://localhost:8080/readyz
curl -i -H "Content-Type: application/json" -d '{"title":"Write spec"}' \
  http://localhost:8080/api/v1/tasks
curl -i 'http://localhost:8080/api/v1/tasks?status=todo&page=1&page_size=20'
```

> Local-only: the `Password=devonly` connection string is for the throwaway container above. In any Azure deployment, the Web App uses Workload Identity → Entra ID token auth and **never** sees a static DB password (see [research.md §4](./research.md)).

---

## 3. Run the MCP server locally against the local Web App

```bash
export TaskApi__BaseUrl="http://localhost:8080/api/v1/"
export MCP_ALLOW_MUTATIONS=true        # dev only — see plan.md Complexity Tracking
dotnet run --project src/McpServer
```

The MCP server starts an HTTP-streaming MCP endpoint on `http://localhost:8081/mcp`. Probe it with the MCP SDK inspector or any MCP-aware client:

```bash
# Discover tools
curl -i -H 'Accept: text/event-stream' http://localhost:8081/mcp \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

# Call list_tasks
curl -i -H 'Accept: text/event-stream' -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_tasks","arguments":{}}}' \
  http://localhost:8081/mcp
```

Set `MCP_ALLOW_MUTATIONS=false` (or unset it) to see the `mutations_disabled` structured error path on `create_task`, `update_task_status`, `update_task_priority`, `delete_task`.

---

## 4. Provision Azure infrastructure (one-time, then idempotent)

```bash
# Discover or plan-to-create AKS (writes infra/aks.discovered.json)
scripts/aks-discover.sh

# Preview the changes
az deployment group what-if \
  --subscription "$AZURE_SUBSCRIPTION_ID" \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters infra/params/dev.bicepparam

# Apply
az deployment group create \
  --subscription "$AZURE_SUBSCRIPTION_ID" \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters infra/params/dev.bicepparam
```

Provisions: UAMI + federated credential, ACR (Basic), PostgreSQL Flexible Server (B1ms), Log Analytics workspace, and AKS (only if `aks.discovered.json` shows none exists).

---

## 5. Build, scan, and push images

```bash
ACR_LOGIN=$(az acr show -n <acrName> -g sainitesh-test --query loginServer -o tsv)
az acr login -n <acrName>

# Pinned base image digest comes from infra/base-images.lock
BASE_IMAGE=$(cat infra/base-images.lock)

docker build --build-arg BASE_IMAGE="$BASE_IMAGE" -f Dockerfile.webapp    -t "$ACR_LOGIN/webapp:$(git rev-parse --short HEAD)" .
docker build --build-arg BASE_IMAGE="$BASE_IMAGE" -f Dockerfile.mcpserver -t "$ACR_LOGIN/mcp-server:$(git rev-parse --short HEAD)" .

# Trivy gate (HIGH/CRITICAL fail the build)
trivy image --severity HIGH,CRITICAL --exit-code 1 "$ACR_LOGIN/webapp:$(git rev-parse --short HEAD)"
trivy image --severity HIGH,CRITICAL --exit-code 1 "$ACR_LOGIN/mcp-server:$(git rev-parse --short HEAD)"

docker push "$ACR_LOGIN/webapp:$(git rev-parse --short HEAD)"
docker push "$ACR_LOGIN/mcp-server:$(git rev-parse --short HEAD)"
```

Capture digests into `deploy/overlays/dev/image-digests.env` (CI does this automatically).

---

## 6. Deploy to AKS

```bash
AKS_NAME=$(jq -r .name infra/aks.discovered.json)
az aks get-credentials -g sainitesh-test -n "$AKS_NAME" --overwrite-existing

# Run migrations Job first, wait for completion
kubectl apply -k deploy/overlays/dev --selector=app=webapp-migrate
kubectl wait --for=condition=complete --timeout=180s job/webapp-migrate -n taskmgr

# Apply the rest
kubectl apply -k deploy/overlays/dev
kubectl rollout status deployment/webapp    -n taskmgr
kubectl rollout status deployment/mcp-server -n taskmgr
```

Verify:

```bash
kubectl -n taskmgr exec deploy/mcp-server -- \
  wget -qO- http://webapp.taskmgr.svc.cluster.local/healthz

kubectl -n taskmgr port-forward svc/mcp-server 8081:8081
# then point an MCP client at http://localhost:8081/mcp
```

---

## 7. Validate the constitution gates in the running cluster

```bash
# Non-root + read-only root FS on both Deployments
kubectl -n taskmgr get deploy webapp     -o jsonpath='{.spec.template.spec.securityContext}{"\n"}{.spec.template.spec.containers[0].securityContext}{"\n"}'
kubectl -n taskmgr get deploy mcp-server -o jsonpath='{.spec.template.spec.securityContext}{"\n"}{.spec.template.spec.containers[0].securityContext}{"\n"}'

# Workload Identity ServiceAccount annotation
kubectl -n taskmgr get sa taskmgr-sa -o yaml | grep azure.workload.identity

# NetworkPolicy restricts MCP egress
kubectl -n taskmgr describe networkpolicy mcp-egress

# MCP_ALLOW_MUTATIONS is true in dev, false in prod
kubectl -n taskmgr get cm mcp-server-config -o jsonpath='{.data.MCP_ALLOW_MUTATIONS}'
```

---

## 8. Tear down (dev only)

```bash
kubectl delete -k deploy/overlays/dev
# Bicep resources persist across deployments; remove explicitly only if desired:
# az group delete -g sainitesh-test  # DESTRUCTIVE — only if the RG is dedicated to this demo
```

---

## Reference

- Plan: [plan.md](./plan.md)
- Research: [research.md](./research.md)
- Data model: [data-model.md](./data-model.md)
- REST contract: [contracts/webapp-openapi.yaml](./contracts/webapp-openapi.yaml)
- MCP tools contract: [contracts/mcp-tools.md](./contracts/mcp-tools.md)
- Constitution: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
