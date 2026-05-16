// Minimal bootstrap. Real wiring (DI, EF Core, Serilog, OpenTelemetry, FluentValidation,
// API endpoints) is added by Phase 2 (T025-T030) and Phase 3 (US1) tasks.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Marker type so WebApplicationFactory<TEntryPoint> in WebApp.Tests can target this assembly.
public partial class Program;

