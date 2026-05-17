using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.Api;

/// <summary>
/// T104 (US3, FR-020, SC-002) — the wire shape of every error response MUST
/// match <c>{"error":{"code","message","details?"}}</c>. This test exercises
/// the live HTTP pipeline for 400 (validation), 404 (missing row), and 405
/// (method not allowed). Unit-level coverage for 409/500 lives in
/// <see cref="ErrorEnvelopeTests"/>; both code paths share the same
/// <c>ErrorEnvelope</c> serializer so the wire shape is identical.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class ErrorEnvelopeUniformityTests
{
    private readonly PostgresFixture _fx;

    public ErrorEnvelopeUniformityTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Validation_400_returns_uniform_envelope()
    {
        var client = _fx.Factory.CreateClient();

        // Empty title triggers FluentValidation -> 400 validation_error.
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertUniformEnvelope(response, expectedCode: "validation_error");
    }

    [Fact]
    public async Task NotFound_404_returns_uniform_envelope()
    {
        var client = _fx.Factory.CreateClient();

        var response = await client.GetAsync(new System.Uri(
            $"/api/v1/tasks/{System.Guid.NewGuid()}", System.UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertUniformEnvelope(response, expectedCode: "not_found");
    }

    [Fact]
    public async Task MethodNotAllowed_405_returns_uniform_envelope()
    {
        var client = _fx.Factory.CreateClient();

        // /api/v1/tasks supports POST + GET but NOT PUT (PUT is only on the {id} route).
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/tasks") { Content = content };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        await AssertUniformEnvelope(response, expectedCode: "method_not_allowed");
    }

    private static async Task AssertUniformEnvelope(HttpResponseMessage response, string expectedCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace(
            $"FR-020: error responses MUST carry an envelope body (HTTP {(int)response.StatusCode})");

        var node = JsonNode.Parse(body);
        node.Should().NotBeNull();
        var error = node!["error"];
        error.Should().NotBeNull("FR-020 requires a top-level \"error\" object");
        error!["code"]!.GetValue<string>().Should().Be(expectedCode);
        error["message"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
    }
}
