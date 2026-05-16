# Feature Specification: Task Manager API & MCP Server

**Feature Branch**: `001-task-manager-api`

**Created**: 2026-05-16

**Status**: Draft

**Input**: User description: "Build a Task Manager system composed of two cooperating services: (A) a Web App that exposes a versioned REST API for managing tasks, and (B) an MCP Server that exposes a curated subset of the Web App API as MCP tools usable by AI agents."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage Tasks via REST API (Priority: P1)

A developer or in-cluster client application needs to programmatically create, read, update, and delete tasks through a stable, versioned HTTP interface. They use the REST API to capture work items (title, description, priority, due date), update progress (status transitions), and remove tasks that are no longer relevant. They also need to filter and paginate task lists so they can build dashboards, reports, or other clients on top of the API.

**Why this priority**: The REST API is the system of record. Without it, no tasks can be stored or retrieved, and the MCP server (Story 2) has nothing to call. It is the minimum viable slice that delivers value on its own — any HTTP client can manage tasks immediately once it ships.

**Independent Test**: Can be fully tested by issuing HTTP requests directly against the API (e.g., `curl`, Postman, or integration tests) to exercise every endpoint, verify validation rules, pagination, filtering, and error shapes — all without the MCP server being present.

**Acceptance Scenarios**:

1. **Given** the API is running and no tasks exist, **When** a client sends `POST /api/v1/tasks` with a valid `title` and optional fields, **Then** the response is `201 Created` with a JSON body containing a server-assigned `id`, the submitted fields, default `status=todo`, default `priority=medium`, and populated `created_at` and `updated_at` timestamps.
2. **Given** a task exists, **When** a client sends `GET /api/v1/tasks/{id}` with the correct id, **Then** the response is `200 OK` with the full task representation in JSON.
3. **Given** several tasks exist with mixed statuses and priorities, **When** a client sends `GET /api/v1/tasks?status=in_progress&priority=high&page=1&page_size=20`, **Then** only tasks matching all filters are returned, the response includes pagination metadata (current page, page size, total count), and at most 20 items are returned.
4. **Given** a task exists, **When** a client sends `PUT /api/v1/tasks/{id}` with a complete, valid task representation, **Then** the task is fully replaced (except `id` and `created_at`), `updated_at` is refreshed, and the response is `200 OK` with the new representation.
5. **Given** a task exists, **When** a client sends `PATCH /api/v1/tasks/{id}` with just `{"status": "done"}`, **Then** only the status changes, other fields are preserved, `updated_at` is refreshed, and the response is `200 OK`.
6. **Given** a task exists, **When** a client sends `DELETE /api/v1/tasks/{id}`, **Then** the response is `204 No Content` and subsequent `GET` on the same id returns `404 Not Found`.
7. **Given** a client submits `POST /api/v1/tasks` with an empty `title` or a `status` outside the allowed set, **Then** the response is `400 Bad Request` with body `{"error": {"code": "validation_error", "message": "...", "details": [...]}}`.
8. **Given** a client requests `GET /api/v1/tasks/{id}` for a non-existent id, **Then** the response is `404 Not Found` with body `{"error": {"code": "not_found", "message": "..."}}`.

---

### User Story 2 - Manage Tasks via AI Agent Through MCP (Priority: P2)

An AI agent (e.g., a coding assistant or workflow automation) discovers and calls MCP tools to manage tasks on behalf of an end user — creating tasks from chat conversations, listing what's currently in progress, marking items done, or adjusting priority. The agent never talks to the database directly; every action flows through the curated MCP tool surface, which in turn calls the REST API. End-to-end traces are possible because a stable correlation id is forwarded on every backing HTTP call.

**Why this priority**: This is the AI-facing differentiator of the system, but it strictly depends on Story 1 being available. It can ship after the REST API is stable and adds value by enabling agentic workflows on top of the same data.

**Independent Test**: Can be tested independently of the AI agent by invoking the MCP server's tools through any MCP client (test harness, MCP inspector, or unit tests) and verifying that each tool call results in the expected REST API call (mocked or live) with the documented correlation id header, and that tool outputs match the documented JSON shapes.

**Acceptance Scenarios**:

1. **Given** the MCP server is running and connected to the REST API, **When** an MCP client invokes `create_task` with `{"title": "Write spec"}`, **Then** the MCP server issues `POST /api/v1/tasks` to the Web App carrying a correlation id header, and returns the created task as JSON to the caller.
2. **Given** tasks exist, **When** an MCP client invokes `list_tasks` with filters (`status`, `priority`, `due_before`, `due_after`) and pagination (`page`, `page_size`), **Then** the server forwards those filters to `GET /api/v1/tasks`, returns the matching page as JSON, and includes pagination metadata so the caller can request subsequent pages.
3. **Given** a task exists, **When** an MCP client invokes `get_task` with its id, **Then** the server returns the task as JSON; if the id is unknown, the tool returns a structured error indicating not found (no exception is leaked).
4. **Given** a task exists, **When** an MCP client invokes `update_task_status` with `{"id": "...", "status": "done"}`, **Then** the server issues `PATCH /api/v1/tasks/{id}` setting only `status` and returns the updated task. The same pattern applies to `update_task_priority`.
5. **Given** any MCP tool call originates from a caller that already supplies a correlation id, **When** the MCP server makes the backing REST call, **Then** the same correlation id is forwarded on the HTTP request so the call can be traced end-to-end; if no correlation id is supplied, the MCP server generates a new one and includes it in both the outbound HTTP call and the tool response metadata.
6. **Given** an MCP client invokes `delete_task` with a valid id, **Then** the server issues `DELETE /api/v1/tasks/{id}` and returns a success indicator; for an unknown id it returns a not-found error without crashing.

---

### User Story 3 - Versioned API Surface Ready for Future Auth (Priority: P3)

Operators and future contributors need confidence that adding authentication, additional fields, or new endpoints later will not break existing clients. The API path is explicitly versioned (`/api/v1/...`), the response shapes are documented and stable, and error responses follow a single consistent envelope, so additive changes (new fields, new endpoints, an auth layer) can be made without forcing v2.

**Why this priority**: This is a design/quality concern rather than a new capability. It does not block initial delivery, but failing to honor it would force a breaking v2 the first time auth or filters are added.

**Independent Test**: Can be verified by reviewing the OpenAPI/contract for the API and confirming: (a) all paths start with `/api/v1/`, (b) the error envelope is uniform across all endpoints, (c) request and response schemas tolerate unknown fields gracefully (clients ignore unknown response fields; server ignores unknown request fields rather than rejecting them), and (d) no endpoint depends on the *absence* of an `Authorization` header.

**Acceptance Scenarios**:

1. **Given** the API specification, **When** any endpoint is invoked, **Then** its path begins with `/api/v1/` and its error responses match the shape `{"error": {"code": "...", "message": "...", "details"?: ...}}`.
2. **Given** a future authentication layer is introduced, **When** an `Authorization` header is added to requests, **Then** existing clients that do not send the header continue to work in v1 deployments (auth is additive, not assumed by the contract).
3. **Given** a request body contains a field the server does not know about, **When** the request is processed, **Then** the server ignores the unknown field rather than returning 400 (forward-compatibility), unless the field conflicts with a validated constraint.

---

### Edge Cases

- **Title length boundaries**: `title` of length 0 → 400; length 1 → accepted; length 200 → accepted; length 201 → 400.
- **Description length boundary**: `description` of length 2000 → accepted; length 2001 → 400.
- **Invalid enum values**: `status` or `priority` outside the allowed set → 400 with `validation_error`.
- **Malformed `due_date`**: non-ISO-date string → 400; valid past date → accepted (past due dates are allowed; the system does not auto-reject them).
- **Pagination bounds**: `page < 1` or `page_size < 1` → 400; `page_size > 100` → 400; a `page` beyond the last page → 200 with an empty `items` array and accurate `total`.
- **Conflicting filter window**: `due_before` earlier than `due_after` → 200 with an empty result set (treated as a legitimate empty window, not an error).
- **PUT with missing required fields**: missing `title` → 400 (PUT is a full replacement and must satisfy all required field rules).
- **PATCH with empty body** or unknown-only fields → 400 `validation_error` (no actionable change requested).
- **PATCH attempting to change `id`, `created_at`, or `updated_at`**: server-managed fields are ignored or rejected; they cannot be set by clients.
- **Delete of an already-deleted task**: second `DELETE` returns 404 (idempotency is not assumed beyond standard HTTP semantics for v1).
- **MCP tool called while Web App is unreachable**: tool returns a structured error indicating upstream unavailability; it does not hang indefinitely and does not silently succeed.
- **MCP `list_tasks` returning a very large page**: capped to the same `page_size` max as the API (100); the tool documents pagination and exposes pagination metadata so agents can iterate.
- **Correlation id missing on inbound MCP call**: MCP server generates one and returns it to the caller as part of tool response metadata, so it can be referenced in logs.
- **Concurrent updates to the same task**: last write wins for v1 (no optimistic concurrency); a future field can be added without breaking the contract.

## Requirements *(mandatory)*

### Functional Requirements

#### Web App REST API — Resource & Schema

- **FR-001**: System MUST persist tasks with the following fields: `id` (server-assigned, stable), `title` (string, required, length 1–200), `description` (string, optional, length 0–2000), `status` (enum: `todo`, `in_progress`, `done`), `priority` (enum: `low`, `medium`, `high`; default `medium`), `due_date` (optional ISO 8601 date), `created_at` (server-set timestamp, immutable), `updated_at` (server-managed timestamp).
- **FR-002**: System MUST default `status` to `todo` and `priority` to `medium` when not supplied on create.
- **FR-003**: System MUST reject any request where `title` is missing, empty, or longer than 200 characters with `400 Bad Request` and a `validation_error` code.
- **FR-004**: System MUST reject any request where `description` exceeds 2000 characters with `400 Bad Request`.
- **FR-005**: System MUST reject any request where `status` or `priority` is not one of the allowed enum values with `400 Bad Request`.
- **FR-006**: System MUST treat `due_date` as an optional ISO 8601 date; malformed values MUST yield `400 Bad Request`.
- **FR-007**: System MUST treat `id`, `created_at`, and `updated_at` as server-managed; client-supplied values for these fields MUST NOT overwrite server state.

#### Web App REST API — Endpoints

- **FR-010**: System MUST expose `POST /api/v1/tasks` to create a task; on success it MUST return `201 Created` with the full created task JSON.
- **FR-011**: System MUST expose `GET /api/v1/tasks` to list tasks with optional filters `status`, `priority`, `due_before`, `due_after`, and pagination via `page` (default 1) and `page_size` (default 20, max 100). The response MUST include the items and pagination metadata (page, page_size, total count).
- **FR-012**: System MUST expose `GET /api/v1/tasks/{id}` to read a single task; unknown id MUST yield `404 Not Found`.
- **FR-013**: System MUST expose `PUT /api/v1/tasks/{id}` for full replacement of a task; all required fields MUST be present in the body; unknown id MUST yield `404 Not Found`.
- **FR-014**: System MUST expose `PATCH /api/v1/tasks/{id}` for partial updates; at minimum `status` and `priority` MUST be patchable; unknown id MUST yield `404 Not Found`; an empty or no-op patch MUST yield `400 Bad Request`.
- **FR-015**: System MUST expose `DELETE /api/v1/tasks/{id}` to delete a task; success MUST return `204 No Content`; unknown id MUST yield `404 Not Found`.
- **FR-016**: System MUST return all successful responses as JSON with `Content-Type: application/json`.
- **FR-017**: System MUST update `updated_at` on every successful mutation (POST, PUT, PATCH).

#### Web App REST API — Errors, Versioning, Extensibility

- **FR-020**: System MUST return all error responses in the shape `{"error": {"code": "<machine-readable>", "message": "<human-readable>", "details"?: <optional structured payload>}}`.
- **FR-021**: System MUST use stable error codes including at least `validation_error` (400), `not_found` (404), and `conflict` (409). Additional codes MAY be added without breaking the contract.
- **FR-022**: System MUST namespace all endpoints under `/api/v1/` so that future breaking changes can be introduced under a new version prefix.
- **FR-023**: System MUST ignore unknown fields in request bodies rather than rejecting them, so additive client/server evolution does not break existing callers.
- **FR-024**: System MUST be designed so an authentication/authorization layer can be added in front of the API later without changing existing request/response schemas, status codes, or paths. In v1, all endpoints are callable by any in-cluster client over HTTP without authentication.

#### MCP Server — Tool Surface

- **FR-030**: MCP server MUST expose exactly these tools: `create_task`, `list_tasks`, `get_task`, `update_task_status`, `update_task_priority`, `delete_task`.
- **FR-031**: Each MCP tool's input schema MUST mirror the input contract of the corresponding REST endpoint (same field names, types, enums, and validation constraints).
- **FR-032**: `list_tasks` MUST accept the same filters as `GET /api/v1/tasks` (`status`, `priority`, `due_before`, `due_after`) and the same pagination parameters (`page`, `page_size` with default 20, max 100).
- **FR-033**: `update_task_status` MUST accept only `id` and `status` and MUST change only `status`. `update_task_priority` MUST accept only `id` and `priority` and MUST change only `priority`.
- **FR-034**: MCP tool outputs MUST be JSON; list outputs MUST include pagination metadata equivalent to the REST API's.

#### MCP Server — Integration & Tracing

- **FR-040**: MCP server MUST call the Web App's REST API for every operation; it MUST NOT access the underlying database or any other storage directly.
- **FR-041**: MCP server MUST forward a stable correlation id on every backing REST call via a documented HTTP header (e.g., `X-Correlation-Id`). If the inbound MCP call carries a correlation id, the same value MUST be used; otherwise the MCP server MUST generate a new one.
- **FR-042**: MCP server MUST include the correlation id in its tool response metadata so callers can reference it in logs and traces.
- **FR-043**: MCP server MUST translate REST error responses into structured tool errors (preserving the `error.code` and `error.message`), rather than raising raw HTTP exceptions to the MCP client.
- **FR-044**: MCP server MUST handle Web App unavailability by returning a structured tool error within a bounded time, not by hanging indefinitely.

### Key Entities

- **Task**: The single managed resource. Attributes: `id`, `title`, `description`, `status` (`todo` | `in_progress` | `done`), `priority` (`low` | `medium` | `high`), `due_date`, `created_at`, `updated_at`. Server owns identity and timestamps; clients own content and lifecycle (status, priority).
- **TaskListPage**: A paginated view over Tasks. Attributes: `items` (array of Task), `page`, `page_size`, `total`. Returned by both the REST list endpoint and the `list_tasks` MCP tool.
- **ErrorEnvelope**: Uniform error response. Attributes: `error.code` (machine-readable string), `error.message` (human-readable string), `error.details` (optional structured payload describing field-level issues).
- **CorrelationId**: A trace identifier propagated from MCP caller → MCP server → Web App so that an end-to-end request can be correlated in logs. Not stored on the Task; lives only on the request/response path.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An external client can perform the full task lifecycle (create → list with filter → read → patch status → delete) against the REST API using only the published contract, with zero need to read source code, in under 10 minutes.
- **SC-002**: 100% of REST endpoints return responses that match the documented schemas, and 100% of error responses match the `{"error": {...}}` envelope, as measured by contract tests.
- **SC-003**: `GET /api/v1/tasks` returns a page of up to 20 tasks in under 300 ms at the 95th percentile under a steady load of 50 concurrent clients with a dataset of 10,000 tasks.
- **SC-004**: `POST /api/v1/tasks` accepts a valid create request and returns the created task in under 200 ms at the 95th percentile under the same steady load.
- **SC-005**: An AI agent using only the MCP tool surface can create, list, change status, change priority, and delete a task end-to-end without a single direct HTTP or database call from the agent, in a single tool-using session.
- **SC-006**: For 100% of MCP tool invocations, the correlation id returned to the MCP caller appears in the Web App's request log for the corresponding backing REST call, enabling end-to-end trace reconstruction.
- **SC-007**: Adding a future authentication layer in front of the API requires zero changes to existing request bodies, response bodies, status codes, or URL paths in `/api/v1/`, as verified by re-running the v1 contract tests unchanged after the auth layer is introduced.
- **SC-008**: Invalid inputs (bad enums, oversize strings, malformed dates, out-of-range pagination) are rejected with `400` and a `validation_error` envelope in 100% of cases covered by the validation test suite — no invalid value reaches the persistence layer.
- **SC-009**: When the Web App is unreachable, every MCP tool call returns a structured error to the caller within 5 seconds; no MCP tool call hangs indefinitely.

## Assumptions

- **Deployment context**: Both services run inside the same Kubernetes cluster. The MCP server reaches the Web App over in-cluster HTTP using a service DNS name; no public ingress is required for v1.
- **Authentication is out of scope for v1**: All callers (including the MCP server) are trusted in-cluster clients. The contract is designed so an auth layer (e.g., bearer tokens, mTLS, or an API gateway) can be added later without breaking existing clients.
- **Persistence**: The Web App owns the task store. The exact storage technology is an implementation detail and is intentionally not constrained by this spec.
- **Time & dates**: Timestamps are stored and returned in UTC using ISO 8601. `due_date` is a calendar date (no time-of-day component required).
- **Concurrency**: Last-writer-wins semantics are acceptable for v1. Optimistic concurrency (e.g., `If-Match`/ETag, or a `version` field) can be added later as an additive change and is therefore out of scope.
- **Idempotency**: Standard HTTP semantics apply (GET/PUT/DELETE are idempotent at the HTTP level; POST is not). Client-supplied idempotency keys are out of scope for v1.
- **Soft delete / audit history**: Out of scope for v1. `DELETE` is a hard delete.
- **MCP discovery**: The MCP server exposes the listed tools through standard MCP tool discovery; AI agents enumerate and call them through their MCP client.
- **Out of scope for v1 (explicit non-goals)**: user accounts, sharing/permissions, attachments, recurring tasks, notifications, and any web UI.
