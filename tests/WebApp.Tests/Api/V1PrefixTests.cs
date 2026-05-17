using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WebApp.Tests.Api;

/// <summary>
/// T103 (US3, FR-022) — every routable endpoint MUST live under <c>/api/v1/</c>
/// except the platform/discovery endpoints (<c>/healthz</c>, <c>/readyz</c>,
/// and the OpenAPI document under <c>/openapi/</c>). The test enumerates the
/// real <see cref="EndpointDataSource"/> built by <c>Program.cs</c>, so any
/// future endpoint accidentally registered outside the v1 prefix will fail
/// CI before it can ship.
/// </summary>
public sealed class V1PrefixTests
{
    private static readonly string[] AllowedNonV1Prefixes =
    [
        "/healthz",
        "/readyz",
        "/openapi",
    ];

    [Fact]
    public void Every_route_is_under_api_v1_or_an_allowlisted_platform_prefix()
    {
        // No DB connection string is configured: the host builds but EF Core
        // queries would fail. EndpointDataSource is populated during host
        // build, before any request executes, so route discovery is safe.
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();

        var endpoints = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        var patterns = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            .Distinct()
            .ToArray();

        patterns.Should().NotBeEmpty(
            "the host must have registered at least the tasks endpoints");

        var offenders = patterns
            .Where(p => !p.StartsWith("/api/v1/", StringComparison.Ordinal)
                        && !AllowedNonV1Prefixes.Any(allow =>
                            p.Equals(allow, StringComparison.Ordinal)
                            || p.StartsWith(allow + "/", StringComparison.Ordinal)))
            .ToArray();

        offenders.Should().BeEmpty(
            "FR-022: every public route must be under /api/v1/ or an allowlisted "
            + "platform prefix (/healthz, /readyz, /openapi/*). Offending routes: "
            + string.Join(", ", offenders));
    }
}
