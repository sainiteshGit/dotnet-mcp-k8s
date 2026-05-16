using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>T042 — GET /api/v1/tasks/{id} returns 200 with the task or 404 with ErrorEnvelope.</summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class GetTaskContractTests
{
    private readonly PostgresFixture _fx;
    public GetTaskContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Get_existing_task_returns_200_with_body()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "Find me" });
        var createdNode = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = createdNode["id"]!.GetValue<string>();

        var got = await client.GetAsync($"/api/v1/tasks/{id}");
        got.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await got.Content.ReadAsStringAsync())!;
        node["id"]!.GetValue<string>().Should().Be(id);
        node["title"]!.GetValue<string>().Should().Be("Find me");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_with_error_envelope()
    {
        var client = _fx.Factory.CreateClient();
        var resp = await client.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        node["error"]!["code"]!.GetValue<string>().Should().Be("not_found");
        node["error"]!["message"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }
}
