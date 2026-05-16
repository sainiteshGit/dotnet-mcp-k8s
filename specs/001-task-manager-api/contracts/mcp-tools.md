# MCP Tools Contract

**MCP spec version**: `2025-06-18` (pinned)
**SDK**: `ModelContextProtocol` (official C# SDK)
**Transport**: HTTP streaming (`WithHttpTransport()`)
**Backing service**: Web App at `http://webapp.taskmgr.svc.cluster.local/api/v1/` (per [contracts/webapp-openapi.yaml](./webapp-openapi.yaml))

All tools are advertised via standard `tools/list`. All invocations go through standard `tools/call`. There are no proprietary protocol extensions (Constitution Principle I).

---

## Mutation gate (Principle II)

The four tools that map to non-GET/HEAD HTTP verbs are **mutation tools**: `create_task`, `update_task_status`, `update_task_priority`, `delete_task`. Each is registered with the MCP host but its execution is gated by the `MCP_ALLOW_MUTATIONS` environment variable:

- `MCP_ALLOW_MUTATIONS=true` → tool executes normally.
- `MCP_ALLOW_MUTATIONS` unset, empty, or any value other than `true` (case-insensitive) → tool returns the structured error below **without** issuing any backing HTTP call.

### Structured `mutations_disabled` response

```json
{
  "isError": true,
  "content": [
    {
      "type": "text",
      "text": "Mutation tools are disabled in this deployment. Set MCP_ALLOW_MUTATIONS=true to enable."
    }
  ],
  "_meta": {
    "correlationId": "<echoed>",
    "error": {
      "code": "mutations_disabled",
      "message": "Mutation tools are disabled in this deployment.",
      "details": {
        "tool": "<tool-name>",
        "remediation": "Set MCP_ALLOW_MUTATIONS=true in the deployment environment."
      }
    }
  }
}
```

Per-overlay defaults (see [plan.md Complexity Tracking](../plan.md#complexity-tracking)):

| Overlay | `MCP_ALLOW_MUTATIONS` | Rationale |
|---|---|---|
| `deploy/overlays/dev`  | `true`  | Demo surface; documented Principle II exception. |
| `deploy/overlays/prod` | `false` | Default-deny per Principle II. CI lint asserts this. |

---

## Correlation-id propagation

Every tool, on every call:

1. Read inbound correlation id from the MCP request's `_meta.correlationId` field. If absent, generate a ULID.
2. Push it onto Serilog's `LogContext` as `CorrelationId` and onto the current OTel span as attribute `correlation.id`.
3. Set HTTP header `X-Correlation-Id: <id>` on the backing call (via `CorrelationIdHandler` `DelegatingHandler`).
4. Include `_meta.correlationId` in the tool response (success or error).

This satisfies FR-041, FR-042, and SC-006.

---

## Error translation (FR-043)

When the backing HTTP call returns a non-2xx response carrying an `ErrorEnvelope`, the tool returns:

```json
{
  "isError": true,
  "content": [ { "type": "text", "text": "<error.message>" } ],
  "_meta": {
    "correlationId": "<echoed>",
    "error": { "code": "<echoed>", "message": "<echoed>", "details": <echoed-or-omitted> }
  }
}
```

When the backing HTTP call fails (timeout, circuit broken, network error) within the Polly resilience budget, the tool returns the `upstream_unavailable` envelope:

```json
{
  "isError": true,
  "content": [ { "type": "text", "text": "Backing API is unavailable. Try again shortly." } ],
  "_meta": {
    "correlationId": "<echoed>",
    "error": {
      "code": "upstream_unavailable",
      "message": "Backing API is unavailable.",
      "details": { "elapsed_ms": <int>, "attempts": <int> }
    }
  }
}
```

Bounded within 5 s (SC-009) by the resilience pipeline in [research.md §2](../research.md).

---

## Tool: `create_task` (mutation)

- **Verb mapped**: `POST /api/v1/tasks`
- **Description**: Create a new task.
- **Input schema** (JSON Schema):

```json
{
  "type": "object",
  "required": ["title"],
  "additionalProperties": false,
  "properties": {
    "title":       { "type": "string", "minLength": 1, "maxLength": 200 },
    "description": { "type": "string", "maxLength": 2000 },
    "status":      { "type": "string", "enum": ["todo", "in_progress", "done"] },
    "priority":    { "type": "string", "enum": ["low", "medium", "high"] },
    "due_date":    { "type": "string", "format": "date" }
  }
}
```

- **Output (success)**:

```json
{
  "isError": false,
  "content": [ { "type": "text", "text": "<title>" } ],
  "structuredContent": { /* full Task object per webapp-openapi.yaml#/components/schemas/Task */ },
  "_meta": { "correlationId": "<id>" }
}
```

## Tool: `list_tasks` (read-only)

- **Verb mapped**: `GET /api/v1/tasks`
- **Description**: List tasks with optional filters and pagination.
- **Input schema**:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "status":     { "type": "string", "enum": ["todo", "in_progress", "done"] },
    "priority":   { "type": "string", "enum": ["low", "medium", "high"] },
    "due_before": { "type": "string", "format": "date" },
    "due_after":  { "type": "string", "format": "date" },
    "page":       { "type": "integer", "minimum": 1, "default": 1 },
    "page_size":  { "type": "integer", "minimum": 1, "maximum": 100, "default": 20 }
  }
}
```

- **Output (success)**: `structuredContent` is a `TaskListPage` (items + page + page_size + total). Pagination metadata is included so agents can iterate.

## Tool: `get_task` (read-only)

- **Verb mapped**: `GET /api/v1/tasks/{id}`
- **Description**: Read a single task by id.
- **Input schema**:

```json
{
  "type": "object",
  "required": ["id"],
  "additionalProperties": false,
  "properties": { "id": { "type": "string", "format": "uuid" } }
}
```

- **Not-found**: returns the structured `not_found` envelope; never raises (FR-043).

## Tool: `update_task_status` (mutation)

- **Verb mapped**: `PATCH /api/v1/tasks/{id}` (body: `{"status": "..."}` only)
- **Description**: Change a task's status. Other fields are not affected.
- **Input schema**:

```json
{
  "type": "object",
  "required": ["id", "status"],
  "additionalProperties": false,
  "properties": {
    "id":     { "type": "string", "format": "uuid" },
    "status": { "type": "string", "enum": ["todo", "in_progress", "done"] }
  }
}
```

## Tool: `update_task_priority` (mutation)

- **Verb mapped**: `PATCH /api/v1/tasks/{id}` (body: `{"priority": "..."}` only)
- **Description**: Change a task's priority. Other fields are not affected.
- **Input schema**:

```json
{
  "type": "object",
  "required": ["id", "priority"],
  "additionalProperties": false,
  "properties": {
    "id":       { "type": "string", "format": "uuid" },
    "priority": { "type": "string", "enum": ["low", "medium", "high"] }
  }
}
```

## Tool: `delete_task` (mutation)

- **Verb mapped**: `DELETE /api/v1/tasks/{id}`
- **Description**: Delete a task. Unknown id → structured `not_found` error.
- **Input schema**:

```json
{
  "type": "object",
  "required": ["id"],
  "additionalProperties": false,
  "properties": { "id": { "type": "string", "format": "uuid" } }
}
```

- **Output (success)**:

```json
{
  "isError": false,
  "content": [ { "type": "text", "text": "deleted" } ],
  "structuredContent": { "deleted": true, "id": "<id>" },
  "_meta": { "correlationId": "<id>" }
}
```
