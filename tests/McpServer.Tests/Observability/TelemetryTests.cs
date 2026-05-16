using FluentAssertions;
using McpServer.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace McpServer.Tests.Observability;

public class TelemetryTests
{
    [Fact]
    public void Activity_source_name_matches_service_name()
    {
        Telemetry.ServiceName.Should().Be("McpServer");
        Telemetry.ActivitySource.Name.Should().Be("McpServer");
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
}
