using System.ComponentModel;
using System.Diagnostics;
using McpServer.Backing;
using McpServer.Mutation;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

#pragma warning disable CA1707 // MCP wire contract requires snake_case parameter names

/// <summary>
/// MCP tool <c>create_task</c> — mutation (T088).
/// Gated by <see cref="MutationGate"/>; when disabled returns the structured
/// <c>mutations_disabled</c> envelope per contracts/mcp-tools.md.
/// </summary>
[McpServerToolType]
public sealed class CreateTaskTool
{
    private readonly ITaskApiClient _client;
    private readonly MutationGate _gate;

    public CreateTaskTool(ITaskApiClient client, MutationGate gate)
    {
        _client = client;
        _gate = gate;
    }

    [McpServerTool(Name = "create_task", Destructive = false, ReadOnly = false)]
    [Description("Create a new task. Requires MCP_ALLOW_MUTATIONS=true on the server.")]
    public async Task<CallToolResult> InvokeAsync(
        [Description("Title (required, 1-200 chars).")] string title,
        [Description("Optional description (up to 2000 chars).")] string? description = null,
        [Description("Initial status: 'todo' | 'in_progress' | 'done'. Defaults to 'todo'.")] string? status = null,
        [Description("Initial priority: 'low' | 'medium' | 'high'. Defaults to 'medium'.")] string? priority = null,
        [Description("Optional due date (ISO-8601 yyyy-MM-dd).")] string? due_date = null,
        CancellationToken cancellationToken = default)
    {
        var corrId = ToolSupport.GetOrCreateCorrelationId();
        if (!_gate.IsEnabled)
        {
            return MutationsDisabledResult.For("create_task", corrId);
        }

        CreateTaskRequestDto dto;
        try
        {
            dto = new CreateTaskRequestDto(
                Title: title,
                Description: description,
                Status: ToolSupport.ParseStatus(status),
                Priority: ToolSupport.ParsePriority(priority),
                DueDate: ToolSupport.ParseDate(due_date));
        }
        catch (ArgumentException ex)
        {
            return ToolSupport.ValidationError(corrId, ex.Message);
        }

        using var _ = CorrelationContext.PushScope(corrId);
        try
        {
            var result = await _client.CreateAsync(dto, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? ErrorTranslator.Success(result.Value!, $"Created task '{result.Value!.Title}' (id={result.Value.Id}).", corrId)
                : ErrorTranslator.FromBackingError(result.Error!, corrId);
        }
        catch (Exception ex) when (ToolSupport.IsUpstreamFailure(ex))
        {
            return ErrorTranslator.UpstreamUnavailable(corrId, attempts: 0, elapsedMs: 0);
        }
    }
}

#pragma warning restore CA1707
