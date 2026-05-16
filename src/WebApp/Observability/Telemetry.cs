using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WebApp.Observability;

/// <summary>
/// OpenTelemetry base wiring for the WebApp (T029).
/// Instrumentation: ASP.NET Core, HttpClient, EF Core. Exporter: OTLP.
/// ActivitySource constant lives here (T031); OTLP exporter scaffold (T032)
/// is folded into <see cref="AddTaskManagerTelemetry"/>.
/// </summary>
public static class Telemetry
{
    /// <summary>Logical service name advertised to the collector.</summary>
    public const string ServiceName = "WebApp";

    /// <summary>Process-wide <see cref="ActivitySource"/> used by application code to start custom spans.</summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    /// <summary>
    /// Adds OpenTelemetry tracing with ASP.NET Core + HttpClient + EF Core instrumentation
    /// and the OTLP exporter. The exporter endpoint is taken from
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (env-var convention) or
    /// <c>OpenTelemetry:Otlp:Endpoint</c> (configuration); if absent, the SDK's default
    /// (localhost:4317 / gRPC) is used. Safe to call from tests.
    /// </summary>
    public static IServiceCollection AddTaskManagerTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                       ?? configuration["OpenTelemetry:Otlp:Endpoint"];

        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySource.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                tracing.AddOtlpExporter(o =>
                {
                    if (!string.IsNullOrWhiteSpace(endpoint))
                    {
                        o.Endpoint = new Uri(endpoint);
                    }
                });
            });

        return services;
    }
}
