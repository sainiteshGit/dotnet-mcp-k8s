using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApp.Api;

namespace WebApp.HealthChecks;

/// <summary>
/// Maps the Kubernetes liveness/readiness endpoints used by the
/// Task Manager Web App (T060/T061). <c>/healthz</c> is a cheap liveness
/// probe; <c>/readyz</c> aggregates registered <see cref="IHealthCheck"/>s
/// and emits <c>{"status":"ready"}</c> on 200 or the uniform
/// <see cref="ErrorEnvelope"/> with code <c>not_ready</c> on 503.
/// </summary>
public static class HealthEndpoints
{
    public const string LivenessPath = "/healthz";
    public const string ReadinessPath = "/readyz";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapTaskManagerHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(LivenessPath, () => Results.Ok(new { status = "live" }))
            .WithName("Liveness")
            .AllowAnonymous();

        endpoints.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            ResponseWriter = WriteReadinessResponseAsync,
        });

        return endpoints;
    }

    private static Task WriteReadinessResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        if (report.Status == HealthStatus.Healthy)
        {
            return context.Response.WriteAsync(
                JsonSerializer.Serialize(new { status = "ready" }, JsonOptions));
        }

        var failed = report.Entries
            .Where(e => e.Value.Status != HealthStatus.Healthy)
            .Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description })
            .ToArray();

        var envelope = new ErrorEnvelope(new ErrorDetails(
            ErrorCode.NotReady,
            "One or more readiness checks failed.",
            new { checks = failed }));

        return context.Response.WriteAsync(JsonSerializer.Serialize(envelope, JsonOptions));
    }
}
