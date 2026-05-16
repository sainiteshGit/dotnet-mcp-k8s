using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using WebApp.Api;
using WebApp.HealthChecks;

namespace WebApp.Tests.HealthChecks;

/// <summary>
/// T061 — when a registered readiness check is unhealthy, /readyz responds
/// 503 with the uniform <see cref="ErrorEnvelope"/> shape and code
/// <c>not_ready</c>.
/// </summary>
public class ReadyzErrorEnvelopeTests
{
    [Fact]
    public async Task Readyz_returns_503_with_not_ready_error_envelope_when_check_unhealthy()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddHealthChecks().AddCheck("postgres", () => HealthCheckResult.Unhealthy("connection refused"));
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapTaskManagerHealthEndpoints());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var envelope = await resp.Content.ReadFromJsonAsync<ErrorEnvelope>();
        envelope.Should().NotBeNull();
        envelope!.Error.Code.Should().Be(ErrorCode.NotReady);
        envelope.Error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Readyz_returns_200_with_status_ready_when_all_checks_healthy()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddHealthChecks().AddCheck("postgres", () => HealthCheckResult.Healthy());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapTaskManagerHealthEndpoints());
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var resp = await client.GetAsync(new Uri("/readyz", UriKind.Relative));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ready\"");
    }
}
