using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>T046 — DELETE returns 204; subsequent GET → 404; second DELETE → 404.</summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class DeleteTaskContractTests
{
    private readonly PostgresFixture _fx;
    public DeleteTaskContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Delete_then_get_then_delete_again_returns_204_404_404()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "ephemeral" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var del1 = await client.DeleteAsync($"/api/v1/tasks/{id}");
        del1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/v1/tasks/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var del2 = await client.DeleteAsync($"/api/v1/tasks/{id}");
        del2.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var node = JsonNode.Parse(await del2.Content.ReadAsStringAsync())!;
        node["error"]!["code"]!.GetValue<string>().Should().Be("not_found");
    }
}
