using System.Globalization;
using System.Net.Http;
using System.Net.Sockets;
using McpServer.Backing;
using McpServer.Pipeline;
using ModelContextProtocol.Protocol;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace McpServer.Tools;

/// <summary>
/// Shared helpers used by all six MCP tools:
/// <list type="bullet">
///   <item><see cref="GetOrCreateCorrelationId"/> — read ambient context or mint a ULID.</item>
///   <item><see cref="ParseStatus"/> / <see cref="ParsePriority"/> / <see cref="ParseDate"/>
///        — snake_case-tolerant parsing of MCP string inputs into typed DTOs.</item>
///   <item><see cref="IsUpstreamFailure"/> — predicate for resilience-pipeline exceptions
///        that should map to <c>upstream_unavailable</c> (FR-044).</item>
/// </list>
/// </summary>
internal static class ToolSupport
{
    public static string GetOrCreateCorrelationId()
        => CorrelationContext.Current ?? UlidGenerator.New();

    public static TaskStatusDto? ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return raw switch
        {
            "todo" => TaskStatusDto.Todo,
            "in_progress" => TaskStatusDto.InProgress,
            "done" => TaskStatusDto.Done,
            _ => throw new ArgumentException(
                $"status must be one of: todo, in_progress, done (got '{raw}').", nameof(raw)),
        };
    }

    public static TaskPriorityDto? ParsePriority(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return raw switch
        {
            "low" => TaskPriorityDto.Low,
            "medium" => TaskPriorityDto.Medium,
            "high" => TaskPriorityDto.High,
            _ => throw new ArgumentException(
                $"priority must be one of: low, medium, high (got '{raw}').", nameof(raw)),
        };
    }

    public static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            throw new ArgumentException(
                $"date must be ISO-8601 yyyy-MM-dd (got '{raw}').", nameof(raw));
        }
        return parsed;
    }

    public static bool IsUpstreamFailure(Exception ex) =>
        ex is HttpRequestException
            or TimeoutRejectedException
            or BrokenCircuitException
            or TaskCanceledException
            or SocketException;

    public static CallToolResult ValidationError(string correlationId, string message)
        => ErrorTranslator.FromBackingError(
            new BackingErrorEnvelope(new BackingErrorDetails(
                Code: "validation_error", Message: message, Details: null)),
            correlationId);
}
