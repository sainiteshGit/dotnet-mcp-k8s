using System.Text.Json;
using FluentAssertions;
using WebApp.Api;

namespace WebApp.Tests.Api;

public class ErrorEnvelopeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ErrorCode_constants_are_stable_strings()
    {
        ErrorCode.ValidationError.Should().Be("validation_error");
        ErrorCode.NotFound.Should().Be("not_found");
        ErrorCode.Conflict.Should().Be("conflict");
        ErrorCode.UpstreamUnavailable.Should().Be("upstream_unavailable");
        ErrorCode.MutationsDisabled.Should().Be("mutations_disabled");
    }

    [Fact]
    public void ErrorEnvelope_serializes_to_required_shape_with_code_and_message()
    {
        var envelope = new ErrorEnvelope(new ErrorDetails(ErrorCode.NotFound, "Task not found", null));

        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("not_found");
        error.GetProperty("message").GetString().Should().Be("Task not found");
        error.TryGetProperty("details", out _).Should().BeFalse(
            "details must be omitted when null per FR-020/FR-021");
    }

    [Fact]
    public void ErrorEnvelope_includes_details_when_provided()
    {
        var details = new { fieldErrors = new[] { new { field = "title", error = "required" } } };
        var envelope = new ErrorEnvelope(new ErrorDetails(ErrorCode.ValidationError, "Invalid input", details));

        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        using var doc = JsonDocument.Parse(json);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("validation_error");
        error.GetProperty("details").GetProperty("fieldErrors")[0].GetProperty("field").GetString()
            .Should().Be("title");
    }
}
