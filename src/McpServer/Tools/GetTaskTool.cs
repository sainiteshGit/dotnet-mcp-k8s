using System.ComponentModel;
using McpServer.Backing;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

/// <summary>
/// MCP tool <c>get_task</c> — read-only (T090). Unknown id returns the backing
/// <c>not_found</c> envelope unchanged (FR-043, scenario 3).
/// </summary>
[McpServerToolType]
public sealed class GetTaskTool
{
    private readonly ITaskApiClient _client;

    public GetTaskTool(ITaskApiClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "get_task", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Fetch a single task by id. Returns a structured not_found error if the id does not exist.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Task id (UUID).")] string id,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();
        if (!Guid.TryParse(id, out var taskId))
        {
            return ToolSupport.ValidationError(corrId, $"id must be a valid UUID (got '{id}').");
        }

        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.GetAsync(taskId, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? ErrorTranslator.Success(result.Value!, $"Task '{result.Value!.Title}' (status={result.Value.Status}).", corrId)
                : ErrorTranslator.FromBackingError(result.Error!, corrId);
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}
