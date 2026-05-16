using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>T045 — PATCH /api/v1/tasks/{id} partial update; empty body → 400.</summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class PatchTaskContractTests
{
    private readonly PostgresFixture _fx;
    public PatchTaskContractTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Patch_status_only_changes_status_and_updatedAt()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "patchme", priority = "low" });
        var createdNode = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var id = createdNode["id"]!.GetValue<string>();
        var origUpdated = createdNode["updated_at"]!.GetValue<string>();

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { status = "in_progress" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await patch.Content.ReadAsStringAsync())!;
        node["status"]!.GetValue<string>().Should().Be("in_progress");
        node["priority"]!.GetValue<string>().Should().Be("low");      // unchanged
        node["title"]!.GetValue<string>().Should().Be("patchme");    // unchanged
        node["updated_at"]!.GetValue<string>().Should().NotBe(origUpdated);
    }

    [Fact]
    public async Task Patch_empty_body_returns_400_validation_error()
    {
        var client = _fx.Factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "x" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var node = JsonNode.Parse(await patch.Content.ReadAsStringAsync())!;
        node["error"]!["code"]!.GetValue<string>().Should().Be("validation_error");
    }
}
