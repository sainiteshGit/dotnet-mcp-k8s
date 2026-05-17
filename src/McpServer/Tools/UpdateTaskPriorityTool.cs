using System.ComponentModel;
using McpServer.Backing;
using McpServer.Mutation;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

#pragma warning disable CA1707 // MCP wire contract requires snake_case parameter names

/// <summary>
/// MCP tool <c>update_task_priority</c> — mutation (T092). Only the priority
/// field is sent to the backing PATCH endpoint.
/// </summary>
[McpServerToolType]
public sealed class UpdateTaskPriorityTool
{
    private readonly ITaskApiClient _client;
    private readonly MutationGate _gate;

    public UpdateTaskPriorityTool(ITaskApiClient client, MutationGate gate)
    {
        _client = client;
        _gate = gate;
    }

    [McpServerTool(Name = "update_task_priority", Destructive = true, ReadOnly = false, Idempotent = true)]
    [Description("Update only the priority of a task. Requires MCP_ALLOW_MUTATIONS=true on the server.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Task id (UUID).")] string id,
        [Description("New priority: 'low' | 'medium' | 'high'.")] string priority,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();
        if (!_gate.IsEnabled)
        {
            return MutationsDisabledResult.For("update_task_priority", corrId);
        }
        if (!Guid.TryParse(id, out var taskId))
        {
            return ToolSupport.ValidationError(corrId, $"id must be a valid UUID (got '{id}').");
        }

        TaskPriorityDto? parsed;
        try
        {
            parsed = ToolSupport.ParsePriority(priority);
        }
        catch (ArgumentException ex)
        {
            return ToolSupport.ValidationError(corrId, ex.Message);
        }
        if (parsed is null)
        {
            return ToolSupport.ValidationError(corrId, "priority is required.");
        }

        var patch = new PatchTaskRequestDto(Priority: parsed);
        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.PatchAsync(taskId, patch, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? ErrorTranslator.Success(result.Value!, $"Task '{result.Value!.Title}' priority is now '{result.Value.Priority}'.", corrId)
                : ErrorTranslator.FromBackingError(result.Error!, corrId);
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}

#pragma warning restore CA1707
