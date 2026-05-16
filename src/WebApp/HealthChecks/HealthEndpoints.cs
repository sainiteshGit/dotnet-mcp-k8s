using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace WebApp.HealthChecks;

/// <summary>
/// Maps the Kubernetes liveness/readiness endpoints used by the
/// Task Manager Web App (T040). The split follows AKS best practice:
/// <c>/healthz</c> is a cheap liveness probe (always 200 if the process is
/// up); <c>/readyz</c> aggregates registered <c>IHealthCheck</c>s so the pod
/// is taken out of service rotation when a dependency (e.g. Postgres) is
/// unavailable. Concrete checks are layered on later in US1 (T061).
/// </summary>
public static class HealthEndpoints
{
    public const string LivenessPath = "/healthz";
    public const string ReadinessPath = "/readyz";

    public static IEndpointRouteBuilder MapTaskManagerHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(LivenessPath, () => Results.Ok(new { status = "live" }))
            .WithName("Liveness")
            .AllowAnonymous();

        endpoints.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            // Default predicate runs every registered check; baseline (no checks)
            // returns 200 because no failures are observed.
        });

        return endpoints;
    }
}
