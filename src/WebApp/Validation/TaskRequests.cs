using System;

namespace WebApp.Validation;

/// <summary>Request body for <c>POST /api/v1/tasks</c>. Unknown fields ignored per FR-023.</summary>
public sealed class CreateTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>Request body for <c>PUT /api/v1/tasks/{id}</c>. Title is required (replaces all fields).</summary>
public sealed class PutTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// Request body for <c>PATCH /api/v1/tasks/{id}</c>. RFC 7396 JSON Merge Patch
/// semantics — only supplied fields are updated. Empty body is invalid (FR-014).
/// </summary>
public sealed class PatchTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>True when at least one field is supplied.</summary>
    public bool HasAny =>
        Title is not null || Description is not null || Status is not null
        || Priority is not null || DueDate is not null;
}

/// <summary>Query-string bindings for <c>GET /api/v1/tasks</c>.</summary>
public sealed class ListTasksQuery
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateOnly? DueBefore { get; set; }
    public DateOnly? DueAfter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
