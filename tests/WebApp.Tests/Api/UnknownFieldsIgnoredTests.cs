using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>
/// T105 (US3, FR-023, scenario 3) — POST/PUT/PATCH bodies that carry extra
/// unknown fields MUST succeed. Forward compatibility requires the JSON
/// deserializer to ignore properties it does not recognize (snake_case
/// options configured in <c>JsonOptionsConfigurator</c>).
/// </summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class UnknownFieldsIgnoredTests
{
    private readonly PostgresFixture _fx;

    public UnknownFieldsIgnoredTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Post_with_unknown_fields_succeeds()
    {
        var client = _fx.Factory.CreateClient();

        var body = new
        {
            title = "with extras",
            // Unknown / future fields must be ignored, not 400.
            future_field = "ignore me",
            nested_unknown = new { foo = 1, bar = "baz" },
            array_unknown = new[] { 1, 2, 3 },
        };

        var response = await client.PostAsJsonAsync("/api/v1/tasks", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        node["title"]!.GetValue<string>().Should().Be("with extras");
    }

    [Fact]
    public async Task Put_with_unknown_fields_succeeds()
    {
        var client = _fx.Factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "put target" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var put = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{id}",
            new { title = "replaced", extra = "ignored", another_one = 42 });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await put.Content.ReadAsStringAsync())!;
        node["title"]!.GetValue<string>().Should().Be("replaced");
    }

    [Fact]
    public async Task Patch_with_unknown_fields_succeeds()
    {
        var client = _fx.Factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "patch target" });
        var id = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/tasks/{id}",
            new { status = "done", unknown_future = "x" });

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var node = JsonNode.Parse(await patch.Content.ReadAsStringAsync())!;
        node["status"]!.GetValue<string>().Should().Be("done");
    }
}
