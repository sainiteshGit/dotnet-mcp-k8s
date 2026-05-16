using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>
/// T041 — POST /api/v1/tasks returns 201 with full task body, populates
/// defaults (status=todo, priority=medium) and timestamps, and emits a
/// Location header pointing at the new resource.
/// </summary>
[Collection("Postgres")]
public sealed class CreateTaskContractTests
{
    private readonly PostgresFixture _fx;

    public CreateTaskContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Create_with_only_title_returns_201_with_defaults_and_location()
    {
        var client = _fx.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "Buy milk" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/api/v1/tasks/");

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        node["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        node["title"]!.GetValue<string>().Should().Be("Buy milk");
        node["status"]!.GetValue<string>().Should().Be("todo");
        node["priority"]!.GetValue<string>().Should().Be("medium");
        node["created_at"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["updated_at"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_with_explicit_status_priority_persists_them()
    {
        var client = _fx.Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new { title = "Ship MVP", status = "in_progress", priority = "high" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        node["status"]!.GetValue<string>().Should().Be("in_progress");
        node["priority"]!.GetValue<string>().Should().Be("high");
    }
}
