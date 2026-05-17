using FluentAssertions;
using McpServer.Backing;
using McpServer.Tools;

namespace McpServer.Tests.Tools;

/// <summary>T076 — Per-tool tests for <see cref="ListTasksTool"/>.</summary>
public class ListTasksToolTests
{
    [Fact]
    public async Task Forwards_filters_to_backing_query()
    {
        ListTasksQuery? captured = null;
        var page = new TaskListPageDto([ToolTestKit.Sample()], 1, 25, 1);
        var client = new FakeTaskApiClient
        {
            OnList = (q, _) => { captured = q; return Task.FromResult(new BackingResult<TaskListPageDto>(200, page, null)); },
        };
        var tool = new ListTasksTool(client);

        var result = await tool.InvokeAsync(status: "in_progress", priority: "high",
            due_before: "2026-06-30", page: 2, page_size: 50);

        result.IsError.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(TaskStatusDto.InProgress);
        captured.Priority.Should().Be(TaskPriorityDto.High);
        captured.DueBefore.Should().Be(new DateOnly(2026, 6, 30));
        captured.Page.Should().Be(2);
        captured.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task Rejects_invalid_date_locally()
    {
        var client = new FakeTaskApiClient
        {
            OnList = (_, _) => throw new InvalidOperationException("client should not be called"),
        };
        var tool = new ListTasksTool(client);

        var result = await tool.InvokeAsync(due_after: "not-a-date");

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("validation_error");
    }

    [Fact]
    public async Task Translates_upstream_failure_to_upstream_unavailable()
    {
        var client = new FakeTaskApiClient
        {
            OnList = (_, _) => throw new TaskCanceledException(),
        };
        var tool = new ListTasksTool(client);

        var result = await tool.InvokeAsync();

        result.IsError.Should().BeTrue();
        result.ErrorCode().Should().Be("upstream_unavailable");
    }
}
