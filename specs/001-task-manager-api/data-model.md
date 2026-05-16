# Phase 1 — Data Model

Scope: the persistence schema owned by the Web App. The MCP server stores nothing.

---

## Entity: `Task`

EF Core entity name: `TaskItem` (the C# type is renamed to avoid clashing with `System.Threading.Tasks.Task`; the JSON/SQL surface remains `task`/`tasks`).

| Field | Type (CLR) | Type (PostgreSQL) | Nullability | Constraints / Default | Notes |
|---|---|---|---|---|---|
| `Id` | `Guid` | `uuid` | NOT NULL | PK; server-assigned (`uuid_generate_v7()` via `pgcrypto` or app-side `Guid.CreateVersion7()`) | Immutable. Exposed as `id` in JSON. |
| `Title` | `string` | `varchar(200)` | NOT NULL | length 1–200; trimmed; FluentValidation `NotEmpty().MaximumLength(200)` | FR-001, FR-003. |
| `Description` | `string?` | `text` | NULL | length 0–2000; FluentValidation `MaximumLength(2000)` | FR-001, FR-004. |
| `Status` | `TaskStatus` (enum) | `text` (stored as canonical lower-case string via EF value converter) | NOT NULL | One of `todo`, `in_progress`, `done`; default `todo` | FR-001, FR-002, FR-005. |
| `Priority` | `TaskPriority` (enum) | `text` (value-converted) | NOT NULL | One of `low`, `medium`, `high`; default `medium` | FR-001, FR-002, FR-005. |
| `DueDate` | `DateOnly?` | `date` | NULL | ISO 8601 calendar date | FR-001, FR-006. |
| `CreatedAt` | `DateTime` (UTC) | `timestamptz` | NOT NULL | server-set on insert; immutable thereafter | FR-001, FR-007. |
| `UpdatedAt` | `DateTime` (UTC) | `timestamptz` | NOT NULL | server-set on insert; refreshed on every successful UPDATE | FR-001, FR-007, FR-017. |

### Indexes

| Index | Columns | Purpose |
|---|---|---|
| `pk_tasks` | `Id` | Primary key (B-tree). |
| `ix_tasks_status_priority_due_date` | `Status, Priority, DueDate` | Backs the common `GET /api/v1/tasks` filter combination (SC-003). |
| `ix_tasks_created_at_desc` | `CreatedAt DESC` | Default list ordering (newest first). |

### State transitions (`Status`)

All transitions are allowed in v1 (no workflow enforcement); the enum is the only constraint:

```text
todo  ⇄  in_progress  ⇄  done
todo  ⇄  done
```

PATCH / PUT may move `Status` to any allowed enum value. (Future enhancement: a workflow guard could be added under a new endpoint or a `transition` operation without breaking the v1 contract.)

### EF Core mapping notes

- Value converters convert each enum ↔ canonical lower-case string (no integer storage — keeps DB self-describing and survives enum re-ordering).
- `Title` and `Description` use `varchar` / `text` (no `nvarchar` — PostgreSQL).
- `RowVersion`/`xmin` concurrency token: **not introduced in v1** (last-writer-wins per spec Assumptions); the migration scaffold leaves room to add `xmin` as a shadow concurrency token later without a contract change.
- `Id` generation: configured `ValueGeneratedNever()` so the application supplies a UUIDv7 at create-time (allows the `Location` header to be set before the DB round-trip and gives time-sortable ids).

---

## Entity (read-only DTO): `TaskListPage`

Not a persisted entity — the response shape of `GET /api/v1/tasks` and the `list_tasks` MCP tool.

| Field | Type | Notes |
|---|---|---|
| `items` | `Task[]` | Up to `page_size` items. |
| `page` | `int` (≥ 1) | Echoed from request (default 1). |
| `page_size` | `int` (1–100) | Echoed from request (default 20). |
| `total` | `long` (≥ 0) | Total matching rows across all pages. |

---

## Entity (cross-cutting DTO): `ErrorEnvelope`

Uniform error shape for every non-2xx response from the Web App and every failed MCP tool call.

| Field | Type | Required | Notes |
|---|---|---|---|
| `error.code` | `string` | yes | Machine-readable; stable across versions. v1 codes: `validation_error`, `not_found`, `conflict`, `upstream_unavailable` (MCP only), `mutations_disabled` (MCP only). Additional codes MAY be added (FR-021). |
| `error.message` | `string` | yes | Human-readable summary. Never includes secrets (redaction middleware applies). |
| `error.details` | `object | array | null` | no | Structured payload (e.g., per-field validation errors). |

### `validation_error.details` shape

```json
{
  "error": {
    "code": "validation_error",
    "message": "One or more fields are invalid.",
    "details": [
      { "field": "title", "code": "required", "message": "title is required." },
      { "field": "description", "code": "max_length", "message": "description must be at most 2000 characters." }
    ]
  }
}
```

---

## Entity (transient): `CorrelationId`

- Not persisted. Lives only on the request/response path.
- Generation: ULID (Crockford-base32, 26 chars).
- Acceptance on input: ULID or UUIDv4 (case-insensitive). Anything else is replaced with a freshly generated ULID and a warning is logged.
- Propagation: header `X-Correlation-Id`; MCP envelope `_meta.correlationId`.
- See [research.md §8](./research.md) for the full propagation contract.

---

## Validation rule summary (single source of truth)

These rules drive both the FluentValidation validators and the OpenAPI schema constraints (kept in sync via contract tests).

- `title`: required, trimmed length 1–200.
- `description`: optional, length 0–2000.
- `status`: optional on create (default `todo`); when present, one of `todo|in_progress|done`.
- `priority`: optional on create (default `medium`); when present, one of `low|medium|high`.
- `due_date`: optional ISO 8601 date (`YYYY-MM-DD`).
- `id`, `created_at`, `updated_at`: server-managed; rejected (silently ignored) on client input — never overwrite server state.
- Pagination: `page` ≥ 1; `1 ≤ page_size ≤ 100`. Out-of-range → `400 validation_error`. Page beyond last → `200` with empty `items` and accurate `total`.
- Filter window: `due_before` may be earlier than `due_after` → `200` with empty result (not an error).
- PATCH: empty body or no-recognised-fields-only body → `400 validation_error`.
- PUT: missing required fields → `400 validation_error`.

---

## Schema bootstrap

Initial migration `0001_initial.cs`:

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE tasks (
    id           uuid        PRIMARY KEY,
    title        varchar(200) NOT NULL,
    description  text        NULL,
    status       text        NOT NULL DEFAULT 'todo',
    priority     text        NOT NULL DEFAULT 'medium',
    due_date     date        NULL,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL,
    CONSTRAINT chk_tasks_status   CHECK (status   IN ('todo','in_progress','done')),
    CONSTRAINT chk_tasks_priority CHECK (priority IN ('low','medium','high')),
    CONSTRAINT chk_tasks_title_len CHECK (char_length(title) BETWEEN 1 AND 200),
    CONSTRAINT chk_tasks_desc_len  CHECK (description IS NULL OR char_length(description) <= 2000)
);

CREATE INDEX ix_tasks_status_priority_due_date ON tasks (status, priority, due_date);
CREATE INDEX ix_tasks_created_at_desc ON tasks (created_at DESC);
```

CHECK constraints duplicate the FluentValidation rules so even a misbehaving client/bypass can't insert invalid rows (defence in depth).
