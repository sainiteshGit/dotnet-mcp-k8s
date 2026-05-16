using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace McpServer.Observability;

/// <summary>
/// OpenTelemetry base wiring for the MCP server (T030).
/// Instrumentation: HttpClient. Exporter: OTLP.
/// ActivitySource constant lives here (T031); OTLP exporter scaffold (T032)
/// is folded into <see cref="AddTaskManagerTelemetry"/>.
/// </summary>
public static class Telemetry
{
    /// <summary>Logical service name advertised to the collector.</summary>
    public const string ServiceName = "McpServer";

    /// <summary>Process-wide <see cref="ActivitySource"/> used by tool handlers to start custom spans.</summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    /// <summary>
    /// Adds OpenTelemetry tracing with HttpClient instrumentation and the OTLP exporter.
    /// Endpoint resolution mirrors the WebApp's <c>Telemetry</c>.
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
                    .AddHttpClientInstrumentation();

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
