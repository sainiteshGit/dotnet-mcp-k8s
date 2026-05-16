using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using WebApp.Tests.Integration;

namespace WebApp.Tests.HealthChecks;

/// <summary>
/// T049 — End-to-end health probes on the real WebApp wired against a live
/// Postgres container. The unit-level health-endpoint tests in
/// <see cref="HealthEndpointsTests"/> exercise the wiring; this asserts the
/// integrated outcome: /healthz is always 200; /readyz is 200 when the DB
/// is reachable.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class HealthEndpointTests
{
    private readonly PostgresFixture _fx;
    public HealthEndpointTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Healthz_returns_200()
    {
        var c = _fx.Factory.CreateClient();
        var r = await c.GetAsync("/healthz");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readyz_returns_200_when_postgres_reachable()
    {
        var c = _fx.Factory.CreateClient();
        var r = await c.GetAsync("/readyz");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
