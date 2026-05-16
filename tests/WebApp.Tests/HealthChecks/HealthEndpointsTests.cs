using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using WebApp.HealthChecks;

namespace WebApp.Tests.HealthChecks;

public class HealthEndpointsTests
{
    private static async Task<IHost> StartAsync(Action<IServiceCollection>? configureServices = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddHealthChecks();
                    configureServices?.Invoke(s);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapTaskManagerHealthEndpoints());
                });
            })
            .StartAsync();
        return host;
    }

    [Fact]
    public async Task Healthz_returns_200_with_no_checks_registered()
    {
        using var host = await StartAsync();
        var client = host.GetTestClient();

        var resp = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readyz_returns_200_when_all_registered_checks_healthy()
    {
        using var host = await StartAsync(s => s.AddHealthChecks().AddCheck("always", () => HealthCheckResult.Healthy()));
        var client = host.GetTestClient();

        var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readyz_returns_503_when_a_check_unhealthy()
    {
        using var host = await StartAsync(s => s.AddHealthChecks().AddCheck("db", () => HealthCheckResult.Unhealthy("down")));
        var client = host.GetTestClient();

        var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Healthz_stays_200_even_when_readyz_check_unhealthy()
    {
        using var host = await StartAsync(s => s.AddHealthChecks().AddCheck("db", () => HealthCheckResult.Unhealthy()));
        var client = host.GetTestClient();

        var resp = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
