using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>
/// T077 — Per-tool tests for <see cref="GetTaskTool"/>. Asserts scenario 3 of
/// FR-043: unknown id yields a structured <c>not_found</c> envelope rather than
/// raising an exception.
/// </summary>
public class GetTaskToolTests
{
    [Fact]
    public async Task Returns_success_envelope_on_200()
    {
        var id = Guid.NewGuid();
        var client = new FakeTaskApiClient
        {
            OnGet = (g, _) => Task.FromResult(new BackingResult<TaskItemDto>(200, ToolTestKit.Sample(id), null)),
        };
        var tool = new GetTaskTool(client);

        var result = await tool.InvokeAsync(id.ToString());

        result.IsError.Should().BeFalse();
        result.CorrelationId().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Unknown_id_returns_structured_not_found_envelope_no_exception()
    {
        var envelope = new BackingErrorEnvelope(new BackingErrorDetails(
            "not_found", "task not found", null));
        var client = new FakeTaskApiClient
        {
            OnGet = (_, _) => Task.FromResult(new BackingResult<TaskItemDto>(404, null, envelope)),
        };
        var tool = new GetTaskTool(client);

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString());

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("not_found");
    }

    [Fact]
    public async Task Invalid_uuid_returns_validation_error_locally()
    {
        var client = new FakeTaskApiClient
        {
            OnGet = (_, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new GetTaskTool(client);

        var result = await tool.InvokeAsync("not-a-uuid");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
    }
}
