using System.Text.Json.Serialization;

namespace McpServer.Backing;

/// <summary>
/// DTOs that mirror <c>contracts/webapp-openapi.yaml</c> exactly. JSON property
/// names are snake_case to match the wire contract; C# property names stay
/// PascalCase. Enums serialize via <see cref="System.Text.Json.JsonSerializerOptions"/>
/// configured in <c>TaskApiClient</c> with snake-case naming policy.
/// </summary>
public sealed record TaskItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("status")] TaskStatusDto Status,
    [property: JsonPropertyName("priority")] TaskPriorityDto Priority,
    [property: JsonPropertyName("due_date")] DateOnly? DueDate,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

public sealed record CreateTaskRequestDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("status"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TaskStatusDto? Status = null,
    [property: JsonPropertyName("priority"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TaskPriorityDto? Priority = null,
    [property: JsonPropertyName("due_date"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? DueDate = null);

public sealed record PatchTaskRequestDto(
    [property: JsonPropertyName("title"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonPropertyName("status"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TaskStatusDto? Status = null,
    [property: JsonPropertyName("priority"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TaskPriorityDto? Priority = null,
    [property: JsonPropertyName("due_date"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? DueDate = null);

public sealed record TaskListPageDto(
    [property: JsonPropertyName("items")] IReadOnlyList<TaskItemDto> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("total")] int Total);

public sealed record ListTasksQuery(
    TaskStatusDto? Status = null,
    TaskPriorityDto? Priority = null,
    DateOnly? DueBefore = null,
    DateOnly? DueAfter = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>
/// Backing API error envelope per <c>webapp-openapi.yaml#/components/schemas/ErrorEnvelope</c>.
/// </summary>
public sealed record BackingErrorEnvelope(
    [property: JsonPropertyName("error")] BackingErrorDetails Error);

public sealed record BackingErrorDetails(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details);

public enum TaskStatusDto { Todo, InProgress, Done }

public enum TaskPriorityDto { Low, Medium, High }
