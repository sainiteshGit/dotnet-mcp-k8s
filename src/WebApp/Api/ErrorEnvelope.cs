using System.Text.Json.Serialization;

namespace WebApp.Api;

/// <summary>
/// Stable, low-cardinality error codes used by both REST (FR-020/FR-021) and
/// the MCP server's structured-error envelope.
/// </summary>
public static class ErrorCode
{
    public const string ValidationError = "validation_error";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string Conflict = "conflict";
    public const string UpstreamUnavailable = "upstream_unavailable";
    public const string MutationsDisabled = "mutations_disabled";
    public const string NotReady = "not_ready";
}

/// <summary>
/// Wire shape: <c>{"error":{"code","message","details?"}}</c> per FR-020/FR-021.
/// </summary>
public sealed record ErrorEnvelope(
    [property: JsonPropertyName("error")] ErrorDetails Error);

public sealed record ErrorDetails(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details);
