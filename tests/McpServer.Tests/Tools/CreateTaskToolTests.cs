using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>
/// T075 — Per-tool tests for <see cref="CreateTaskTool"/>.
/// Covers: mutation-gate disabled → mutations_disabled envelope;
/// backing 201 → success envelope with correlation id; backing 400 → echoed
/// validation error; upstream exception → upstream_unavailable envelope.
/// </summary>
public class CreateTaskToolTests
{
    [Fact]
    public async Task Returns_mutations_disabled_when_gate_off_without_calling_client()
    {
        var calls = 0;
        var client = new FakeTaskApiClient
        {
            OnCreate = (_, _) => { calls++; return Task.FromResult(new BackingResult<TaskItemDto>(201, null, null)); },
        };
        var tool = new CreateTaskTool(client, ToolTestKit.Gate(enabled: false));

        var result = await tool.InvokeAsync("t");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("mutations_disabled");
        calls.Should().Be(0, "the gate must short-circuit before any HTTP call");
    }

    [Fact]
    public async Task Returns_success_envelope_with_correlation_id_on_201()
    {
        var created = ToolTestKit.Sample(title: "buy milk");
        var client = new FakeTaskApiClient
        {
            OnCreate = (_, _) => Task.FromResult(new BackingResult<TaskItemDto>(201, created, null)),
        };
        var tool = new CreateTaskTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync("buy milk", priority: "high", due_date: "2026-06-01");

        result.IsError.Should().BeFalse();
        result.CorrelationId().Should().NotBeNullOrWhiteSpace();
        result.StructuredContent.Should().NotBeNull();
    }

    [Fact]
    public async Task Echoes_backing_validation_error_envelope()
    {
        var envelope = new BackingErrorEnvelope(new BackingErrorDetails(
            "validation_error", "title is required", null));
        var client = new FakeTaskApiClient
        {
            OnCreate = (_, _) => Task.FromResult(new BackingResult<TaskItemDto>(400, null, envelope)),
        };
        var tool = new CreateTaskTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync("");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
    }

    [Fact]
    public async Task Translates_upstream_failure_to_upstream_unavailable()
    {
        var client = new FakeTaskApiClient
        {
            OnCreate = (_, _) => throw new HttpRequestException("boom"),
        };
        var tool = new CreateTaskTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync("t");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("upstream_unavailable");
    }

    [Fact]
    public async Task Rejects_invalid_status_locally_without_calling_client()
    {
        var calls = 0;
        var client = new FakeTaskApiClient
        {
            OnCreate = (_, _) => { calls++; return Task.FromResult(new BackingResult<TaskItemDto>(201, null, null)); },
        };
        var tool = new CreateTaskTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync("t", status: "WRONG");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
        calls.Should().Be(0);
    }
}
