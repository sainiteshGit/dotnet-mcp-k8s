using System.ComponentModel;
using McpServer.Backing;
using McpServer.Mutation;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

#pragma warning disable CA1707 // MCP wire contract requires snake_case parameter names

/// <summary>
/// MCP tool <c>update_task_status</c> — mutation (T091). Only the status field
/// is sent to the backing PATCH endpoint, regardless of other inputs.
/// </summary>
[McpServerToolType]
public sealed class UpdateTaskStatusTool
{
    private readonly ITaskApiClient _client;
    private readonly MutationGate _gate;

    public UpdateTaskStatusTool(ITaskApiClient client, MutationGate gate)
    {
        _client = client;
        _gate = gate;
    }

    [McpServerTool(Name = "update_task_status", Destructive = true, ReadOnly = false, Idempotent = true)]
    [Description("Update only the status of a task. Requires MCP_ALLOW_MUTATIONS=true on the server.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Task id (UUID).")] string id,
        [Description("New status: 'todo' | 'in_progress' | 'done'.")] string status,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();
        if (!_gate.IsEnabled)
        {
            return MutationsDisabledResult.For("update_task_status", corrId);
        }
        if (!Guid.TryParse(id, out var taskId))
        {
            return ToolSupport.ValidationError(corrId, $"id must be a valid UUID (got '{id}').");
        }

        TaskStatusDto? parsed;
        try
        {
            parsed = ToolSupport.ParseStatus(status);
        }
        catch (ArgumentException ex)
        {
            return ToolSupport.ValidationError(corrId, ex.Message);
        }
        if (parsed is null)
        {
            return ToolSupport.ValidationError(corrId, "status is required.");
        }

        var patch = new PatchTaskRequestDto(Status: parsed);
        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.PatchAsync(taskId, patch, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? ErrorTranslator.Success(result.Value!, $"Task '{result.Value!.Title}' status is now '{result.Value.Status}'.", corrId)
                : ErrorTranslator.FromBackingError(result.Error!, corrId);
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}

#pragma warning restore CA1707
