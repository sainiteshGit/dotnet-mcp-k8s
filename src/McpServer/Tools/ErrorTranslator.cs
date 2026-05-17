using System.Text.Json;
using System.Text.Json.Nodes;
using McpServer.Backing;
using ModelContextProtocol.Protocol;

namespace McpServer.Tools;

/// <summary>
/// Maps backing API outcomes to MCP <see cref="CallToolResult"/> envelopes:
/// <list type="bullet">
///   <item><see cref="Success{T}"/> — backing 2xx → <c>isError:false</c> with structured content.</item>
///   <item><see cref="FromBackingError"/> — backing non-2xx with <see cref="BackingErrorEnvelope"/> → <c>isError:true</c> echoing the envelope (FR-043).</item>
///   <item><see cref="UpstreamUnavailable"/> — exception inside the Polly budget → <c>upstream_unavailable</c> envelope (FR-044).</item>
/// </list>
/// All envelopes carry <c>_meta.correlationId</c> per contracts/mcp-tools.md.
/// </summary>
public static class ErrorTranslator
{
    public const string UpstreamUnavailableCode = "upstream_unavailable";
    public const string UpstreamUnavailableMessage = "Backing API is unavailable.";
    public const string UpstreamUnavailableUserText = "Backing API is unavailable. Try again shortly.";

    public static CallToolResult Success<T>(T structured, string textSummary, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = textSummary }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, BackingJsonOptions.Default),
            Meta = new JsonObject
            {
                ["correlationId"] = correlationId,
            },
        };
    }

    public static CallToolResult FromBackingError(BackingErrorEnvelope envelope, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var error = new JsonObject
        {
            ["code"] = envelope.Error.Code,
            ["message"] = envelope.Error.Message,
        };
        if (envelope.Error.Details is not null)
        {
            error["details"] = JsonSerializer.SerializeToNode(envelope.Error.Details);
        }
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = envelope.Error.Message }],
            Meta = new JsonObject
            {
                ["correlationId"] = correlationId,
                ["error"] = error,
            },
        };
    }

    public static CallToolResult UpstreamUnavailable(string correlationId, int attempts, int elapsedMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = UpstreamUnavailableUserText }],
            Meta = new JsonObject
            {
                ["correlationId"] = correlationId,
                ["error"] = new JsonObject
                {
                    ["code"] = UpstreamUnavailableCode,
                    ["message"] = UpstreamUnavailableMessage,
                    ["details"] = new JsonObject
                    {
                        ["elapsed_ms"] = elapsedMs,
                        ["attempts"] = attempts,
                    },
                },
            },
        };
    }
}
