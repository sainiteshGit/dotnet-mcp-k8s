using System.Text.Json;
using FluentAssertions;
using WebApp.Api;

namespace WebApp.Tests.Api;

public class JsonOptionsConfiguratorTests
{
    private sealed class Sample
    {
        public string TaskTitle { get; init; } = "";
        public int PageSize { get; init; }
        public SampleStatus Status { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private enum SampleStatus { Todo, InProgress, Done }

    private static JsonSerializerOptions Configured()
    {
        var o = new JsonSerializerOptions();
        JsonOptionsConfigurator.ConfigureSnakeCase(o);
        return o;
    }

    [Fact]
    public void Property_names_serialize_as_snake_case()
    {
        var json = JsonSerializer.Serialize(new Sample
        {
            TaskTitle = "buy milk",
            PageSize = 20,
            Status = SampleStatus.InProgress,
            CreatedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        }, Configured());

        json.Should().Contain("\"task_title\":\"buy milk\"");
        json.Should().Contain("\"page_size\":20");
        json.Should().Contain("\"created_at\":");
    }

    [Fact]
    public void Enum_values_serialize_as_snake_case_strings()
    {
        var json = JsonSerializer.Serialize(new Sample { Status = SampleStatus.InProgress }, Configured());
        json.Should().Contain("\"status\":\"in_progress\"");
    }

    [Fact]
    public void Unknown_request_fields_are_ignored_on_deserialize()
    {
        const string payload = """{"task_title":"x","unknown_field":42}""";
        var act = () => JsonSerializer.Deserialize<Sample>(payload, Configured());
        act.Should().NotThrow();
    }
}
