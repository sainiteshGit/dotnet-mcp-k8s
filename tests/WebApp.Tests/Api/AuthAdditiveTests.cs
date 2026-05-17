using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>
/// T106 (US3, FR-024, SC-007) — the v1 surface MUST be auth-additive: every
/// endpoint accepts requests both with and without an <c>Authorization</c>
/// header. When a future auth release lands, adding bearer-token validation
/// will be a pure server-side change that does not break existing clients.
///
/// Why this test exists: without it, a future contributor could accidentally
/// add an <c>[Authorize]</c> attribute or a global middleware that rejects
/// unauthenticated requests, silently breaking SC-007.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class AuthAdditiveTests
{
    private readonly PostgresFixture _fx;

    public AuthAdditiveTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task All_endpoints_succeed_both_with_and_without_Authorization_header()
    {
        await ExerciseFullLifecycle(addBearer: false);
        await ExerciseFullLifecycle(addBearer: true);
    }

    private async Task ExerciseFullLifecycle(bool addBearer)
    {
        var client = _fx.Factory.CreateClient();
        if (addBearer)
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "dummy-not-validated");
        }

        // POST
        var create = await client.PostAsJsonAsync(
            "/api/v1/tasks",
            new { title = addBearer ? "auth-on" : "auth-off" });
        create.StatusCode.Should().Be(HttpStatusCode.Created,
            "FR-024: POST must succeed with or without Authorization");
        var id = JsonNode.Parse(await create.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        // GET list
        var list = await client.GetAsync(new System.Uri("/api/v1/tasks", System.UriKind.Relative));
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET by id
        var get = await client.GetAsync(new System.Uri($"/api/v1/tasks/{id}", System.UriKind.Relative));
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        // PUT
        var put = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{id}",
            new { title = "put", status = "in_progress", priority = "high" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        // PATCH
        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { status = "done" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        // DELETE
        using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{id}");
        var delete = await client.SendAsync(delReq);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Healthz and readyz should also be reachable in both modes.
        var healthz = await client.GetAsync(new System.Uri("/healthz", System.UriKind.Relative));
        healthz.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
