// Minimal bootstrap. Real wiring (DI, EF Core, FluentValidation, API endpoints,
// OpenTelemetry exporter) is added by Phase 2 (T029-T030) and Phase 3 (US1) tasks.
// Already wired here: correlation-id middleware (T028) and Serilog redaction
// enricher (T025).

using Serilog;
using WebApp.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<RedactionEnricher>()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

builder.Services.AddTaskManagerTelemetry(builder.Configuration);

var app = builder.Build();

app.UseCorrelationId();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Marker type so WebApplicationFactory<TEntryPoint> in WebApp.Tests can target this assembly.
public partial class Program;

