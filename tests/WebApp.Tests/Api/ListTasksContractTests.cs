using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>T043 — GET /api/v1/tasks with filters and pagination.</summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class ListTasksContractTests
{
    private readonly PostgresFixture _fx;
    public ListTasksContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task List_returns_paged_envelope_with_items_page_pageSize_total()
    {
        var client = _fx.Factory.CreateClient();
        // seed a couple of rows
        await client.PostAsJsonAsync("/api/v1/tasks", new { title = "alpha", priority = "low" });
        await client.PostAsJsonAsync("/api/v1/tasks", new { title = "beta", priority = "high" });

        var resp = await client.GetAsync("/api/v1/tasks?page=1&page_size=20");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        node["items"]!.AsArray().Count.Should().BeGreaterThan(0);
        node["page"]!.GetValue<int>().Should().Be(1);
        node["page_size"]!.GetValue<int>().Should().Be(20);
        node["total"]!.GetValue<long>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task List_filter_by_priority_returns_only_matching()
    {
        var client = _fx.Factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/tasks", new { title = "p-low", priority = "low" });
        await client.PostAsJsonAsync("/api/v1/tasks", new { title = "p-high", priority = "high" });

        var resp = await client.GetAsync("/api/v1/tasks?priority=high&page=1&page_size=100");
        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        foreach (var item in node["items"]!.AsArray())
        {
            item!["priority"]!.GetValue<string>().Should().Be("high");
        }
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page_size=0")]
    [InlineData("page_size=101")]
    public async Task List_rejects_out_of_range_pagination(string query)
    {
        var client = _fx.Factory.CreateClient();
        var resp = await client.GetAsync($"/api/v1/tasks?{query}");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        node["error"]!["code"]!.GetValue<string>().Should().Be("validation_error");
    }
}
