using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace McpServer.Mutation;

/// <summary>
/// Builds the structured <c>mutations_disabled</c> envelope mandated by
/// <c>contracts/mcp-tools.md#structured-mutations_disabled-response</c>.
/// Mutation tools call <see cref="For"/> when <see cref="MutationGate.IsEnabled"/>
/// is false. No backing HTTP call is made.
/// </summary>
public static class MutationsDisabledResult
{
    public const string ErrorCode = "mutations_disabled";

    public const string UserFacingText =
        "Mutation tools are disabled in this deployment. " +
        "Set MCP_ALLOW_MUTATIONS=true to enable.";

    public const string EnvelopeMessage =
        "Mutation tools are disabled in this deployment.";

    public const string Remediation =
        "Set MCP_ALLOW_MUTATIONS=true in the deployment environment.";

    public static CallToolResult For(string toolName, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        // Stub returns empty/wrong shape so MutationGateTests fail until impl is real.
        return new CallToolResult { IsError = false, Content = [] };
    }

    internal static JsonObject BuildMetaForTests(string toolName, string correlationId) =>
        new()
        {
            ["correlationId"] = correlationId,
            ["error"] = new JsonObject
            {
                ["code"] = ErrorCode,
                ["message"] = EnvelopeMessage,
                ["details"] = new JsonObject
                {
                    ["tool"] = toolName,
                    ["remediation"] = Remediation,
                },
            },
        };
}
