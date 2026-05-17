using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpServer.Tests.HealthChecks;

/// <summary>
/// T081 — Integration tests for the MCP server health endpoints (T095).
/// Spins the full pipeline via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// pointed at an ephemeral WireMock instance acting as the backing API.
/// </summary>
public class HealthEndpointTests : IAsyncLifetime, IDisposable
{
    private WireMockServer _backing = null!;
    private WebApplicationFactory<McpServer.Program> _factory = null!;

    public Task InitializeAsync()
    {
        _backing = WireMockServer.Start();
        _factory = new WebApplicationFactory<McpServer.Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("BACKING_API_BASE_URL", _backing.Url);
            });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        _backing.Stop();
        _backing.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public async Task Liveness_returns_200_regardless_of_upstream()
    {
        using var client = _factory.CreateClient();

        using var resp = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_returns_200_when_upstream_healthy()
    {
        _backing.Given(Request.Create().WithPath("/healthz").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(200));
        using var client = _factory.CreateClient();

        using var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_returns_503_when_upstream_returns_5xx()
    {
        _backing.Given(Request.Create().WithPath("/healthz").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(503));
        using var client = _factory.CreateClient();

        using var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
