// Minimal bootstrap. Real wiring (MCP transport, typed HttpClient + Polly resilience,
// tool registrations, OpenTelemetry exporter) is added by Phase 2 (T030) and
// Phase 4 (US2) tasks. Already wired here: Serilog redaction enricher (T026).

using McpServer.Observability;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<RedactionEnricher>()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

var host = builder.Build();
await host.RunAsync();
