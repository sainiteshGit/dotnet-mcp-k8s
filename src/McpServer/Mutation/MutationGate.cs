using Microsoft.Extensions.Configuration;

namespace McpServer.Mutation;

/// <summary>
/// Constitution Principle II — Read-Only by Default.
///
/// Reads <c>MCP_ALLOW_MUTATIONS</c> from configuration (env var). Mutation
/// tools (<c>create_task</c>, <c>update_task_status</c>, <c>update_task_priority</c>,
/// <c>delete_task</c>) MUST check <see cref="IsEnabled"/> BEFORE issuing any
/// backing HTTP call. When false, the tool returns
/// <see cref="MutationsDisabledResult.For"/> instead.
///
/// Only the literal value <c>"true"</c> (case-insensitive) enables mutations.
/// Any other value, empty, or unset → mutations disabled.
/// </summary>
public sealed class MutationGate
{
    public const string ConfigKey = "MCP_ALLOW_MUTATIONS";

    private readonly bool _enabled;

    public MutationGate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // Stub: hard-coded false until impl reads configuration[ConfigKey].
        _enabled = false;
    }

    public bool IsEnabled => _enabled;
}
