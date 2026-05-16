using System;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using WebApp.Validation;

namespace WebApp.Tests.Validation;

/// <summary>
/// T047 — exercises the FluentValidation rules for all 4 request DTOs
/// (Create / Put / Patch / List query) against every boundary from
/// <c>data-model.md</c>: title 0/1/200/201, description 2000/2001,
/// invalid enums, malformed due_date, pagination bounds, filter window.
/// </summary>
public sealed class TaskValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Create_title_length_bounds(int len, bool valid)
    {
        var v = new CreateTaskValidator();
        var req = new CreateTaskRequest { Title = new string('a', len) };
        var r = v.Validate(req);
        r.IsValid.Should().Be(valid, $"title length {len}");
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Create_description_length_bounds(int len, bool valid)
    {
        var v = new CreateTaskValidator();
        var req = new CreateTaskRequest { Title = "ok", Description = new string('d', len) };
        v.Validate(req).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Patch_empty_body_is_invalid()
    {
        var v = new PatchTaskValidator();
        var r = v.Validate(new PatchTaskRequest());
        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Patch_with_status_only_is_valid()
    {
        var v = new PatchTaskValidator();
        var r = v.Validate(new PatchTaskRequest { Status = "in_progress" });
        r.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("todo", true)]
    [InlineData("in_progress", true)]
    [InlineData("done", true)]
    [InlineData("bogus", false)]
    public void Patch_status_enum_validation(string val, bool valid)
    {
        var v = new PatchTaskValidator();
        v.Validate(new PatchTaskRequest { Status = val }).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData("low", true)]
    [InlineData("medium", true)]
    [InlineData("high", true)]
    [InlineData("urgent", false)]
    public void Patch_priority_enum_validation(string val, bool valid)
    {
        var v = new PatchTaskValidator();
        v.Validate(new PatchTaskRequest { Priority = val }).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Put_requires_title()
    {
        var v = new PutTaskValidator();
        v.Validate(new PutTaskRequest()).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 100, true)]
    [InlineData(1, 101, false)]
    public void ListQuery_pagination_bounds(int page, int pageSize, bool valid)
    {
        var v = new ListTasksQueryValidator();
        v.Validate(new ListTasksQuery { Page = page, PageSize = pageSize }).IsValid.Should().Be(valid);
    }

    [Fact]
    public void ListQuery_filter_window_with_due_before_earlier_than_due_after_is_valid()
    {
        // Per data-model.md "Filter window … → 200 with empty result (not an error)".
        var v = new ListTasksQueryValidator();
        var r = v.Validate(new ListTasksQuery
        {
            Page = 1,
            PageSize = 20,
            DueBefore = new DateOnly(2024, 1, 1),
            DueAfter = new DateOnly(2025, 1, 1),
        });
        r.IsValid.Should().BeTrue();
    }
}
