using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApp.Persistence;

/// <summary>
/// T061 — readiness probe that opens a connection to Postgres and runs
/// <c>SELECT 1</c>. Failure flips <c>/readyz</c> to HTTP 503 so AKS takes the
/// pod out of rotation; <c>/healthz</c> stays liveness-only.
/// </summary>
public sealed class PostgresReadinessCheck : IHealthCheck
{
    private readonly TaskDbContext _db;

    public PostgresReadinessCheck(TaskDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
            return canConnect
                ? HealthCheckResult.Healthy("postgres reachable")
                : HealthCheckResult.Unhealthy("postgres CanConnect returned false");
        }
#pragma warning disable CA1031 // health checks must never throw out of the probe path
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("postgres unreachable", ex);
        }
#pragma warning restore CA1031
    }
}
