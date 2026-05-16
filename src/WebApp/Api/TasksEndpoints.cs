using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Results;
using WebApp.Domain;
using WebApp.Persistence;
using WebApp.Validation;

namespace WebApp.Api;

/// <summary>
/// Maps the <c>/api/v1/tasks</c> endpoint group (T056). Endpoints mirror the
/// OpenAPI contract in <c>specs/001-task-manager-api/contracts/webapp-openapi.yaml</c>:
/// POST 201+Location, GET list/by-id, PUT, PATCH (RFC 7396 merge semantics),
/// DELETE. Validation failures → 400 <c>validation_error</c>; missing rows →
/// 404 <c>not_found</c>; both via the <see cref="ErrorEnvelope"/> wire shape.
/// </summary>
public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/tasks").WithTags("Tasks");

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapPut("/{id:guid}", PutAsync);
        group.MapPatch("/{id:guid}", PatchAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateTaskRequest body,
        IValidator<CreateTaskRequest> validator,
        ITaskRepository repo,
        CancellationToken ct)
    {
        var v = await validator.ValidateAsync(body, ct).ConfigureAwait(false);
        if (!v.IsValid)
        {
            return ValidationError(v);
        }

        var item = new TaskItem
        {
            Title = body.Title!,
            Description = body.Description,
            Status = ParseStatus(body.Status) ?? Domain.TaskStatus.Todo,
            Priority = ParsePriority(body.Priority) ?? Domain.TaskPriority.Medium,
            DueDate = body.DueDate,
        };
        var created = await repo.CreateAsync(item, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/tasks/{created.Id}", ToDto(created));
    }

    private static async Task<IResult> ListAsync(
        HttpRequest request,
        IValidator<ListTasksQuery> validator,
        ITaskRepository repo,
        CancellationToken ct)
    {
        var q = request.Query;
        var query = new ListTasksQuery
        {
            Status = q["status"].ToString() is { Length: > 0 } s ? s : null,
            Priority = q["priority"].ToString() is { Length: > 0 } p ? p : null,
            DueBefore = TryParseDate(q["due_before"]),
            DueAfter = TryParseDate(q["due_after"]),
            Page = TryParseInt(q["page"], 1),
            PageSize = TryParseInt(q["page_size"], 20),
        };
        var v = await validator.ValidateAsync(query, ct).ConfigureAwait(false);
        if (!v.IsValid)
        {
            return ValidationError(v);
        }

        var page = await repo.ListAsync(
            ParseStatus(query.Status), ParsePriority(query.Priority),
            query.DueBefore, query.DueAfter,
            query.Page, query.PageSize, ct).ConfigureAwait(false);

        return Results.Ok(new
        {
            items = page.Items.Select(ToDto).ToArray(),
            page = page.Page,
            page_size = page.PageSize,
            total = page.Total,
        });
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ITaskRepository repo, CancellationToken ct)
    {
        var item = await repo.GetAsync(id, ct).ConfigureAwait(false);
        return item is null ? NotFound(id) : Results.Ok(ToDto(item));
    }

    private static async Task<IResult> PutAsync(
        Guid id,
        PutTaskRequest body,
        IValidator<PutTaskRequest> validator,
        ITaskRepository repo,
        CancellationToken ct)
    {
        var v = await validator.ValidateAsync(body, ct).ConfigureAwait(false);
        if (!v.IsValid)
        {
            return ValidationError(v);
        }

        var updated = await repo.ReplaceAsync(id, t =>
        {
            t.Title = body.Title!;
            t.Description = body.Description;
            t.Status = ParseStatus(body.Status) ?? Domain.TaskStatus.Todo;
            t.Priority = ParsePriority(body.Priority) ?? Domain.TaskPriority.Medium;
            t.DueDate = body.DueDate;
        }, ct).ConfigureAwait(false);

        return updated is null ? NotFound(id) : Results.Ok(ToDto(updated));
    }

    private static async Task<IResult> PatchAsync(
        Guid id,
        HttpRequest request,
        IValidator<PatchTaskRequest> validator,
        ITaskRepository repo,
        CancellationToken ct)
    {
        // Parse raw JSON so we can distinguish "field omitted" from "field
        // set to null" (RFC 7396 merge-patch semantics required by FR-014).
        JsonObject? root;
        try
        {
            var node = await JsonNode.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
            root = node as JsonObject;
        }
        catch (JsonException)
        {
            return ValidationError("Request body is not valid JSON.");
        }

        if (root is null || root.Count == 0)
        {
            return ValidationError("Request body must be a non-empty JSON object.");
        }

        var body = new PatchTaskRequest();
        if (root.ContainsKey("title"))
        {
            body.Title = root["title"]?.GetValue<string>();
        }
        if (root.ContainsKey("description"))
        {
            body.Description = root["description"]?.GetValue<string>();
        }
        if (root.ContainsKey("status"))
        {
            body.Status = root["status"]?.GetValue<string>();
        }
        if (root.ContainsKey("priority"))
        {
            body.Priority = root["priority"]?.GetValue<string>();
        }
        if (root.ContainsKey("due_date"))
        {
            var due = root["due_date"];
            body.DueDate = due is null
                ? null
                : DateOnly.Parse(due.GetValue<string>(), CultureInfo.InvariantCulture);
        }

        var recognised =
            root.ContainsKey("title") || root.ContainsKey("description") ||
            root.ContainsKey("status") || root.ContainsKey("priority") ||
            root.ContainsKey("due_date");
        if (!recognised)
        {
            return ValidationError("At least one recognised field must be supplied.");
        }

        var v = await validator.ValidateAsync(body, ct).ConfigureAwait(false);
        if (!v.IsValid)
        {
            return ValidationError(v);
        }

        var patched = await repo.PatchAsync(id, t =>
        {
            if (root.ContainsKey("title") && body.Title is not null)
            {
                t.Title = body.Title;
            }
            if (root.ContainsKey("description"))
            {
                t.Description = body.Description;
            }
            if (root.ContainsKey("status") && body.Status is not null)
            {
                t.Status = ParseStatus(body.Status)!.Value;
            }
            if (root.ContainsKey("priority") && body.Priority is not null)
            {
                t.Priority = ParsePriority(body.Priority)!.Value;
            }
            if (root.ContainsKey("due_date"))
            {
                t.DueDate = body.DueDate;
            }
        }, ct).ConfigureAwait(false);

        return patched is null ? NotFound(id) : Results.Ok(ToDto(patched));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ITaskRepository repo, CancellationToken ct)
    {
        var ok = await repo.DeleteAsync(id, ct).ConfigureAwait(false);
        return ok ? Results.NoContent() : NotFound(id);
    }

    // ---- helpers ----------------------------------------------------------

    private static Domain.TaskStatus? ParseStatus(string? s) => s switch
    {
        "todo" => Domain.TaskStatus.Todo,
        "in_progress" => Domain.TaskStatus.InProgress,
        "done" => Domain.TaskStatus.Done,
        _ => null,
    };

    private static Domain.TaskPriority? ParsePriority(string? p) => p switch
    {
        "low" => Domain.TaskPriority.Low,
        "medium" => Domain.TaskPriority.Medium,
        "high" => Domain.TaskPriority.High,
        _ => null,
    };

    private static string StatusWire(Domain.TaskStatus s) => s switch
    {
        Domain.TaskStatus.InProgress => "in_progress",
        Domain.TaskStatus.Done => "done",
        _ => "todo",
    };

    private static string PriorityWire(Domain.TaskPriority p) => p switch
    {
        Domain.TaskPriority.High => "high",
        Domain.TaskPriority.Low => "low",
        _ => "medium",
    };

    private static object ToDto(TaskItem t) => new
    {
        id = t.Id,
        title = t.Title,
        description = t.Description,
        status = StatusWire(t.Status),
        priority = PriorityWire(t.Priority),
        due_date = t.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        created_at = t.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        updated_at = t.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
    };

    private static DateOnly? TryParseDate(Microsoft.Extensions.Primitives.StringValues raw)
    {
        var s = raw.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return null;
        }
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int TryParseInt(Microsoft.Extensions.Primitives.StringValues raw, int fallback)
    {
        var s = raw.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return fallback;
        }
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
    }

    private static IResult ValidationError(ValidationResult v)
    {
        var details = v.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }).ToArray();
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(ErrorCode.ValidationError, "Request validation failed.", new { fieldErrors = details })),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult ValidationError(string message)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(ErrorCode.ValidationError, message, null)),
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult NotFound(Guid id)
    {
        return Results.Json(
            new ErrorEnvelope(new ErrorDetails(ErrorCode.NotFound, $"Task '{id}' was not found.", null)),
            statusCode: StatusCodes.Status404NotFound);
    }
}
