using System.ComponentModel;
using System.Text.Json.Nodes;
using McpServer.Backing;
using McpServer.Mutation;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

/// <summary>
/// MCP tool <c>delete_task</c> — destructive mutation (T093). Unknown id
/// returns the backing <c>not_found</c> envelope (FR-043, scenario 3).
/// </summary>
[McpServerToolType]
public sealed class DeleteTaskTool
{
    private readonly ITaskApiClient _client;
    private readonly MutationGate _gate;

    public DeleteTaskTool(ITaskApiClient client, MutationGate gate)
    {
        _client = client;
        _gate = gate;
    }

    [McpServerTool(Name = "delete_task", Destructive = true, ReadOnly = false, Idempotent = true)]
    [Description("Delete a task by id. Requires MCP_ALLOW_MUTATIONS=true on the server.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Task id (UUID).")] string id,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();
        if (!_gate.IsEnabled)
        {
            return MutationsDisabledResult.For("delete_task", corrId);
        }
        if (!Guid.TryParse(id, out var taskId))
        {
            return ToolSupport.ValidationError(corrId, $"id must be a valid UUID (got '{id}').");
        }

        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.DeleteAsync(taskId, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return ErrorTranslator.FromBackingError(result.Error!, corrId);
            }

            return new CallToolResult
            {
                IsError = false,
                Content = [new TextContentBlock { Text = $"Deleted task {taskId}." }],
                StructuredContent = System.Text.Json.JsonSerializer.SerializeToElement(
                    new { id = taskId, deleted = true }, BackingJsonOptions.Default),
                Meta = new JsonObject { ["correlationId"] = corrId },
            };
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}
