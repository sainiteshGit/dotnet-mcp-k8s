using System.Diagnostics;
using McpServer.Backing;
using McpServer.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Direct ILogger calls are clearer than LoggerMessage delegates for these one-shot probe paths.

namespace McpServer.HealthChecks;

/// <summary>
/// Health endpoints for Kubernetes (T095):
/// <list type="bullet">
///   <item><c>/healthz</c> — liveness: always 200 if the process is up.</item>
///   <item><c>/readyz</c> — readiness: 200 only when the backing API
///        <c>/healthz</c> answers within the timeout, else 503 with a structured
///        body so kubectl/operators get an actionable hint.</item>
/// </list>
/// </summary>
public static class HealthEndpoints
{
    public static readonly TimeSpan ReadinessProbeTimeout = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
                 .WithName("liveness");

        endpoints.MapGet("/readyz", async (
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("McpServer.HealthChecks");
            using var http = httpClientFactory.CreateClient(nameof(ITaskApiClient));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ReadinessProbeTimeout);

            var sw = Stopwatch.StartNew();
            try
            {
                using var resp = await http.GetAsync(new Uri("healthz", UriKind.Relative), cts.Token).ConfigureAwait(false);
                sw.Stop();
                if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 500)
                {
                    return Results.Ok(new { status = "ready", upstream_ms = sw.ElapsedMilliseconds });
                }
                logger.LogWarning("Readiness probe: upstream returned {Status} after {Ms}ms", (int)resp.StatusCode, sw.ElapsedMilliseconds);
                return Results.Json(new { status = "not_ready", reason = "upstream_unhealthy", upstream_status = (int)resp.StatusCode, upstream_ms = sw.ElapsedMilliseconds }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                sw.Stop();
                logger.LogWarning(ex, "Readiness probe: upstream unreachable after {Ms}ms", sw.ElapsedMilliseconds);
                return Results.Json(new { status = "not_ready", reason = "upstream_unreachable", upstream_ms = sw.ElapsedMilliseconds }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("readiness");

        return endpoints;
    }
}
