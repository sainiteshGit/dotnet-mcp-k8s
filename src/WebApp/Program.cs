// Program entry point for the Task Manager Web App.
//
// T057 wires the EF Core DbContext, repositories, validators, JSON conventions,
// /api/v1/tasks endpoints (T056), built-in .NET 10 OpenAPI (T058 — Swashbuckle
// 7.x is incompatible with .NET 10, so we use Microsoft.AspNetCore.OpenApi
// plus Scalar.AspNetCore for the UI in Development), Postgres Entra-ID auth
// (T059), health endpoints (T060/T061), and supports a `--migrate` CLI mode
// used by the Kubernetes pre-deploy Job (T065).
//
// Tests target this class via `WebApplicationFactory<Program>` and inject
// `ConnectionStrings:Postgres` to point at a Testcontainers Postgres instance,
// so the connection-string resolution path here MUST honour that key when
// present.

using Azure.Identity;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using WebApp.Api;
using WebApp.Azure;
using WebApp.HealthChecks;
using WebApp.Observability;
using WebApp.Persistence;
using WebApp.Validation;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog + redaction enricher, T025) -----------------------------
builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<RedactionEnricher>()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// --- Telemetry (OTel tracing + EF Core instrumentation, T029) -----------------
builder.Services.AddTaskManagerTelemetry(builder.Configuration);

// --- JSON conventions for minimal API (snake_case, T057) ----------------------
builder.Services.ConfigureHttpJsonOptions(o => JsonOptionsConfigurator.ConfigureSnakeCase(o.SerializerOptions));

// --- Persistence (T053-T055, T059) --------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

builder.Services.AddDbContext<TaskDbContext>((sp, opt) =>
{
    var cs = ResolvePostgresConnectionString(sp.GetRequiredService<IConfiguration>());
    opt.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations_history"));
});

// --- Validation (T054) --------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<CreateTaskValidator>();

// --- Health checks (T060/T061) ------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<PostgresReadinessCheck>("postgres", tags: ["ready"]);

// --- OpenAPI (T058) -----------------------------------------------------------
// .NET 10 built-in. Swashbuckle 7.x is incompatible with net10.0.
builder.Services.AddOpenApi("v1");

var app = builder.Build();

// --- `--migrate` CLI branch (T057 / consumed by webapp-migrate Job, T065) -----
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
    Log.CloseAndFlush();
    Environment.Exit(0);
}

app.UseCorrelationId();

// Spec available in every environment so prod can introspect; UI only in dev.
app.MapOpenApi("/openapi/{documentName}.json");
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(o => o.WithOpenApiRoutePattern("/openapi/{documentName}.json"));
}

app.MapTaskManagerHealthEndpoints();
app.MapTasksEndpoints();

await app.RunAsync().ConfigureAwait(false);

// ------------------------------------------------------------------------------
static string ResolvePostgresConnectionString(IConfiguration config)
{
    // 1) Tests / local dev: explicit ConnectionStrings:Postgres wins.
    var explicitConn = config.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(explicitConn))
    {
        return explicitConn;
    }

    // 2) Cluster / prod: build an Entra-ID authenticated connection string.
    var host = config["POSTGRES_HOST"];
    var database = config["POSTGRES_DATABASE"] ?? "taskmgr";
    var username = config["POSTGRES_USER"];

    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
    {
        throw new InvalidOperationException(
            "Postgres connection is not configured. Set ConnectionStrings__Postgres for tests, " +
            "or POSTGRES_HOST + POSTGRES_USER (+ optional POSTGRES_DATABASE) for the cluster.");
    }

    var credentialOptions = new DefaultAzureCredentialOptions();
    if (WorkloadIdentity.TryGetClientId(out var clientId))
    {
        credentialOptions.ManagedIdentityClientId = clientId;
    }
    var credential = new DefaultAzureCredential(credentialOptions);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    return PostgresConnectionStringBuilder
        .BuildAsync(host!, database, username!, credential, cts.Token)
        .GetAwaiter()
        .GetResult();
}

// Marker type so WebApplicationFactory<TEntryPoint> in WebApp.Tests can target this assembly.
public partial class Program;
