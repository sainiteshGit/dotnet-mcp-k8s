using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using McpServer.Backing;
using McpServer.Mutation;
using McpServer.Tools;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;

namespace McpServer.Tests.Tools;

/// <summary>
/// Shared in-test stub of <see cref="ITaskApiClient"/>. Each method is wired
/// to a delegate the test sets up; missing delegates throw to surface
/// mis-routed calls.
/// </summary>
internal sealed class FakeTaskApiClient : ITaskApiClient
{
    public Func<CreateTaskRequestDto, CancellationToken, Task<BackingResult<TaskItemDto>>>? OnCreate { get; set; }
    public Func<ListTasksQuery, CancellationToken, Task<BackingResult<TaskListPageDto>>>? OnList { get; set; }
    public Func<Guid, CancellationToken, Task<BackingResult<TaskItemDto>>>? OnGet { get; set; }
    public Func<Guid, PatchTaskRequestDto, CancellationToken, Task<BackingResult<TaskItemDto>>>? OnPatch { get; set; }
    public Func<Guid, CancellationToken, Task<BackingResult<bool>>>? OnDelete { get; set; }

    public Task<BackingResult<TaskItemDto>> CreateAsync(CreateTaskRequestDto req, CancellationToken ct = default)
        => OnCreate!.Invoke(req, ct);
    public Task<BackingResult<TaskListPageDto>> ListAsync(ListTasksQuery q, CancellationToken ct = default)
        => OnList!.Invoke(q, ct);
    public Task<BackingResult<TaskItemDto>> GetAsync(Guid id, CancellationToken ct = default)
        => OnGet!.Invoke(id, ct);
    public Task<BackingResult<TaskItemDto>> PatchAsync(Guid id, PatchTaskRequestDto p, CancellationToken ct = default)
        => OnPatch!.Invoke(id, p, ct);
    public Task<BackingResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
        => OnDelete!.Invoke(id, ct);
}

/// <summary>Helpers shared across per-tool tests.</summary>
internal static class ToolTestKit
{
    public static MutationGate Gate(bool enabled) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MCP_ALLOW_MUTATIONS"] = enabled ? "true" : "false",
            })
            .Build());

    public static TaskItemDto Sample(Guid? id = null, string title = "buy milk",
        TaskStatusDto status = TaskStatusDto.Todo, TaskPriorityDto priority = TaskPriorityDto.Medium)
        => new(
            Id: id ?? Guid.NewGuid(),
            Title: title,
            Description: null,
            Status: status,
            Priority: priority,
            DueDate: null,
            CreatedAt: DateTimeOffset.Parse("2026-05-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt: DateTimeOffset.Parse("2026-05-16T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    public static string CorrelationId(this CallToolResult r) =>
        ((JsonObject)r.Meta!)["correlationId"]!.GetValue<string>();

    public static string ErrorCode(this CallToolResult r) =>
        ((JsonObject)((JsonObject)r.Meta!)["error"]!)["code"]!.GetValue<string>();
}
