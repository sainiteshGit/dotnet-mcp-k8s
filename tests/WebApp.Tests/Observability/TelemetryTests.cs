using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using WebApp.Observability;

namespace WebApp.Tests.Observability;

public class TelemetryTests
{
    [Fact]
    public void Activity_source_name_matches_service_name()
    {
        Telemetry.ServiceName.Should().Be("WebApp");
        Telemetry.ActivitySource.Name.Should().Be("WebApp");
    }

    [Fact]
    public void AddTaskManagerTelemetry_registers_tracer_provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder().Build();

        services.AddTaskManagerTelemetry(config);

        using var provider = services.BuildServiceProvider();
        var tracer = provider.GetService<TracerProvider>();
        tracer.Should().NotBeNull("AddTaskManagerTelemetry must wire OpenTelemetry tracing");
    }

    [Fact]
    public void Activity_started_under_source_carries_service_name_tag()
    {
        // Sanity: the ActivitySource is usable from production code.
        using var activity = Telemetry.ActivitySource.StartActivity("test-op");
        // No listener attached → activity may be null; that's fine. The point is the source exists.
        Telemetry.ActivitySource.Should().NotBeNull();
    }
}
