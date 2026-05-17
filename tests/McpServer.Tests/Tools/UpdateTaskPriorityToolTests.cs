using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>T079 — Per-tool tests for <see cref="UpdateTaskPriorityTool"/>.</summary>
public class UpdateTaskPriorityToolTests
{
    [Fact]
    public async Task Returns_mutations_disabled_when_gate_off()
    {
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, _, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new UpdateTaskPriorityTool(client, ToolTestKit.Gate(enabled: false));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString(), "high");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("mutations_disabled");
    }

    [Fact]
    public async Task Sends_only_priority_field_in_patch_body()
    {
        PatchTaskRequestDto? captured = null;
        var id = Guid.NewGuid();
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, p, _) =>
            {
                captured = p;
                return Task.FromResult(new BackingResult<TaskItemDto>(200, ToolTestKit.Sample(id, priority: TaskPriorityDto.High), null));
            },
        };
        var tool = new UpdateTaskPriorityTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync(id.ToString(), "high");

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Priority.Should().Be(TaskPriorityDto.High);
        captured.Status.Should().BeNull("only the priority field is mutated by this tool");
        captured.Title.Should().BeNull();
        captured.Description.Should().BeNull();
        captured.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_priority_returns_validation_error()
    {
        var client = new FakeTaskApiClient
        {
            OnPatch = (_, _, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new UpdateTaskPriorityTool(client, ToolTestKit.Gate(enabled: true));

        var result = await tool.InvokeAsync(Guid.NewGuid().ToString(), "URGENT");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
    }
}
