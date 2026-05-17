using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>
/// T080 — Per-tool tests for <see cref="DeleteTaskTool"/>. Unknown id surfaces
/// the structured backing <c>not_found</c> envelope (FR-043 scenario 3); the
/// tool never raises.
/// </summary>
public class DeleteTaskToolTests
{
    [Fact]
    public async Task Returns_mutations_disabled_when_gate_off()
    {
        var client = new FakeTaskApiClient
        {
            OnDelete = (_, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new DeleteTaskTool(client, ToolTestKit.Gate(enabled: false));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString());

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("mutations_disabled");
    }

    [Fact]
    public async Task Returns_success_envelope_on_204()
    {
        var id = Guid.NewGuid();
        var client = new FakeTaskApiClient
        {
            OnDelete = (_, _) => Task.FromResult(new BackingResult<bool>(204, true, null)),
        };
        var tool = new DeleteTaskTool(client, ToolTestKit.Gate(enabled: true));

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
            OnDelete = (_, _) => Task.FromResult(new BackingResult<bool>(404, false, envelope)),
        };
        var tool = new DeleteTaskTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString());

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("not_found");
    }
}
