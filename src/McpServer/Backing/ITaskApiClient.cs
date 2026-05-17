namespace McpServer.Backing;

/// <summary>
/// Typed-client abstraction over the WebApp REST surface. One method per
/// MCP tool. Implementations forward through Polly resilience and the
/// <see cref="Pipeline.CorrelationIdHandler"/>.
///
/// <para>Error semantics: callers receive a <see cref="BackingResult{T}"/>
/// that wraps either the success payload or the structured backing
/// <see cref="BackingErrorEnvelope"/>. Network/timeout failures bubble up as
/// exceptions and are translated by <c>ErrorTranslator</c> at the tool layer
/// into <c>upstream_unavailable</c>.</para>
/// </summary>
public interface ITaskApiClient
{
    Task<BackingResult<TaskItemDto>> CreateAsync(CreateTaskRequestDto request, CancellationToken ct = default);
    Task<BackingResult<TaskListPageDto>> ListAsync(ListTasksQuery query, CancellationToken ct = default);
    Task<BackingResult<TaskItemDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<BackingResult<TaskItemDto>> PatchAsync(Guid id, PatchTaskRequestDto patch, CancellationToken ct = default);
    Task<BackingResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Either-style result: <see cref="Value"/> is set on success, otherwise
/// <see cref="Error"/> carries the structured backing envelope.
/// </summary>
public sealed record BackingResult<T>(int StatusCode, T? Value, BackingErrorEnvelope? Error)
{
    public bool IsSuccess => Error is null;
}
