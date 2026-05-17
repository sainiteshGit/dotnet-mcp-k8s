using System.Text.Json.Nodes;
using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;
using ModelContextProtocol.Protocol;

namespace McpServer.Tests.Tools;

/// <summary>
/// T087 — ErrorTranslator envelope shapes per contracts/mcp-tools.md.
/// </summary>
public class ErrorTranslatorTests
{
    private const string CorrId = "TESTCORRELATIONID0123456789";

    [Fact]
    public void Success_carries_structured_content_and_correlation_id()
    {
        var dto = new TaskItemDto(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title: "buy milk",
            Description: null,
            Status: TaskStatusDto.Todo,
            Priority: TaskPriorityDto.Medium,
            DueDate: null,
            CreatedAt: new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));

        var r = ErrorTranslator.Success(dto, "buy milk", CorrId);

        r.IsError.Should().BeFalse();
        ((TextContentBlock)r.Content[0]).Text.Should().Be("buy milk");
        r.StructuredContent.HasValue.Should().BeTrue();
        r.Meta!["correlationId"]!.GetValue<string>().Should().Be(CorrId);
    }

    [Fact]
    public void FromBackingError_echoes_code_message_and_correlation_id()
    {
        var env = new BackingErrorEnvelope(new BackingErrorDetails(
            Code: "not_found", Message: "no such task", Details: null));

        var r = ErrorTranslator.FromBackingError(env, CorrId);

        r.IsError.Should().BeTrue();
        ((TextContentBlock)r.Content[0]).Text.Should().Be("no such task");
        var err = (JsonObject)r.Meta!["error"]!;
        err["code"]!.GetValue<string>().Should().Be("not_found");
        err["message"]!.GetValue<string>().Should().Be("no such task");
        r.Meta["correlationId"]!.GetValue<string>().Should().Be(CorrId);
    }

    [Fact]
    public void UpstreamUnavailable_envelope_matches_contract()
    {
        var r = ErrorTranslator.UpstreamUnavailable(CorrId, attempts: 3, elapsedMs: 4870);

        r.IsError.Should().BeTrue();
        ((TextContentBlock)r.Content[0]).Text.Should().Be(ErrorTranslator.UpstreamUnavailableUserText);
        var err = (JsonObject)r.Meta!["error"]!;
        err["code"]!.GetValue<string>().Should().Be("upstream_unavailable");
        var details = (JsonObject)err["details"]!;
        details["attempts"]!.GetValue<int>().Should().Be(3);
        details["elapsed_ms"]!.GetValue<int>().Should().Be(4870);
    }
}
