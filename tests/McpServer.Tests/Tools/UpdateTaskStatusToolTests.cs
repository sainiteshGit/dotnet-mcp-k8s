using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>T078 — Per-tool tests for <see cref="UpdateTaskStatusTool"/>.</summary>
public class UpdateTaskStatusToolTests
{
    [Fact]
    public async Task Returns_mutations_disabled_when_gate_off()
    {
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, _, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new UpdateTaskStatusTool(client, ToolTestKit.Gate(enabled: false));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString(), "done");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("mutations_disabled");
    }

    [Fact]
    public async Task Sends_only_status_field_in_patch_body()
    {
        PatchTaskRequestDto? captured = null;
        var id = Guid.NewGuid();
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, p, _) =>
            {
                captured = p;
                return Task.FromResult(new BackingResult<TaskItemDto>(200, ToolTestKit.Sample(id, status: TaskStatusDto.Done), null));
            },
        };
        var tool = new UpdateTaskStatusTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync(id.ToString(), "done");

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(TaskStatusDto.Done);
        captured.Priority.Should().BeNull("only the status field is mutated by this tool");
        captured.Title.Should().BeNull();
        captured.Description.Should().BeNull();
        captured.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_status_returns_validation_error()
    {
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, _, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new UpdateTaskStatusTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString(), "WRONG");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
    }
}
