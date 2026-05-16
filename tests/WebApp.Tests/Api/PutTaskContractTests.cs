using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>T044 — PUT /api/v1/tasks/{id} replaces all fields; missing required → 400.</summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class PutTaskContractTests
{
    private readonly PostgresFixture _fx;
    public PutTaskContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Put_replaces_all_fields_and_returns_200()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "orig", priority = "low" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var put = await client.PutAsJsonAsync($"/api/v1/tasks/{id}", new
        {
            title = "replaced",
            description = "now with body",
            status = "done",
            priority = "high",
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await put.Content.ReadAsStringAsync())!;
        node["title"]!.GetValue<string>().Should().Be("replaced");
        node["description"]!.GetValue<string>().Should().Be("now with body");
        node["status"]!.GetValue<string>().Should().Be("done");
        node["priority"]!.GetValue<string>().Should().Be("high");
    }

    [Fact]
    public async Task Put_missing_title_returns_400_validation_error()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "orig" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var put = await client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { description = "no title" });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var node = JsonNode.Parse(await put.Content.ReadAsStringAsync())!;
        node["error"]!["code"]!.GetValue<string>().Should().Be("validation_error");
    }
}
