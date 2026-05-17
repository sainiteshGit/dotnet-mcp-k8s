using System.ComponentModel;
using McpServer.Backing;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

#pragma warning disable CA1707 // MCP wire contract requires snake_case parameter names

/// <summary>
/// MCP tool <c>list_tasks</c> — read-only (T089). Returns a paged list of tasks
/// with optional filters; forwards filters verbatim to the backing API.
/// </summary>
[McpServerToolType]
public sealed class ListTasksTool
{
    private readonly ITaskApiClient _client;

    public ListTasksTool(ITaskApiClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "list_tasks", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("List tasks with optional filters; results are paged.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Filter by status: 'todo' | 'in_progress' | 'done'.")] string? status = null,
        [Description("Filter by priority: 'low' | 'medium' | 'high'.")] string? priority = null,
        [Description("Only tasks with due_date strictly before this ISO-8601 date (yyyy-MM-dd).")] string? due_before = null,
        [Description("Only tasks with due_date on/after this ISO-8601 date (yyyy-MM-dd).")] string? due_after = null,
        [Description("Page number, 1-based. Defaults to 1.")] int? page = null,
        [Description("Page size, 1-100. Defaults to 25.")] int? page_size = null,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();

        ListTasksQuery query;
        try
        {
            query = new ListTasksQuery(
                Status: ToolSupport.ParseStatus(status),
                Priority: ToolSupport.ParsePriority(priority),
                DueBefore: ToolSupport.ParseDate(due_before),
                DueAfter: ToolSupport.ParseDate(due_after),
                Page: page,
                PageSize: page_size);
        }
        catch (ArgumentException ex)
        {
            return ToolSupport.ValidationError(corrId, ex.Message);
        }

        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.ListAsync(query, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? ErrorTranslator.Success(result.Value!, $"Returned {result.Value!.Items.Count} of {result.Value.Total} task(s) (page {result.Value.Page}).", corrId)
                : ErrorTranslator.FromBackingError(result.Error!, corrId);
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}

#pragma warning restore CA1707
