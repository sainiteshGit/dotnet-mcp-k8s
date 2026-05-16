using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Observability;

public class CorrelationIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Header = "X-Correlation-Id";
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Inbound_correlation_id_is_echoed_on_response()
    {
        var client = _factory.CreateClient();
        const string inbound = "01HXYZ0123456789ABCDEFGHJK";

        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(Header, inbound);
        var resp = await client.SendAsync(req);

        resp.Headers.TryGetValues(Header, out var values).Should().BeTrue();
        values!.Single().Should().Be(inbound);
    }

    [Fact]
    public async Task Missing_correlation_id_is_generated_and_echoed()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/health");

        resp.Headers.TryGetValues(Header, out var values).Should().BeTrue();
        var id = values!.Single();
        id.Should().NotBeNullOrWhiteSpace();
        id.Length.Should().BeInRange(16, 64,
            "generated correlation id is a ULID (26 chars) or UUID (36 chars)");
    }
}
