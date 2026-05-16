namespace McpServer.Pipeline;

/// <summary>
/// Ambient correlation-id store for the MCP server (T033).
/// MCP tool handlers call <see cref="PushScope"/> on entry so that any
/// outbound HTTP call made via the typed <c>TaskApiClient</c> picks up the
/// id through <see cref="CorrelationIdHandler"/> and forwards it as
/// <c>X-Correlation-Id</c>. AsyncLocal so the value flows across awaits
/// without contaminating sibling requests.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _current = new();

    /// <summary>The correlation id active on the current logical call, or <c>null</c>.</summary>
    public static string? Current => _current.Value;

    /// <summary>
    /// Sets <see cref="Current"/> to <paramref name="id"/> for the lifetime of the
    /// returned scope. Restores the previous value on dispose.
    /// </summary>
    public static IDisposable PushScope(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var previous = _current.Value;
        _current.Value = id;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public Scope(string? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _current.Value = _previous;
            _disposed = true;
        }
    }
}
