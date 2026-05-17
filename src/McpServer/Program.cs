// MCP Server entrypoint (T094).
//
// Hosts the Model Context Protocol server over Streamable HTTP transport,
// pinning the protocol version to "2025-06-18" (Principle I). Exposes six
// tools (registered via [McpServerToolType] assembly scan) backed by a typed
// HttpClient with Polly resilience pointed at the WebApp REST surface
// (BACKING_API_BASE_URL). Mutations require MCP_ALLOW_MUTATIONS=true.

using McpServer.Backing;
using McpServer.HealthChecks;
using McpServer.Mutation;
using McpServer.Observability;
using McpServer.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

#pragma warning disable CA1848, CA1873 // Single startup log line; LoggerMessage delegate overkill.

var builder = WebApplication.CreateBuilder(args);

// Structured logging with redaction (T026).
builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<RedactionEnricher>()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// OpenTelemetry traces + metrics.
builder.Services.AddTaskManagerTelemetry(builder.Configuration);

// Mutation gate (Principle II — read-only by default).
builder.Services.AddSingleton<MutationGate>();

// Typed backing-API client + Polly resilience (T085).
var backingBaseUrl = builder.Configuration["BACKING_API_BASE_URL"]
    ?? throw new InvalidOperationException(
        "BACKING_API_BASE_URL is required (e.g. http://webapp.taskmgr.svc.cluster.local).");
builder.Services.AddTaskApiClient(new Uri(backingBaseUrl));

// MCP server with Streamable HTTP transport, scanning this assembly for
// [McpServerToolType] classes (CreateTaskTool, ListTasksTool, GetTaskTool,
// UpdateTaskStatusTool, UpdateTaskPriorityTool, DeleteTaskTool).
builder.Services
    .AddMcpServer(opts =>
    {
        opts.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "task-manager-mcp",
            Version = "1.0.0",
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Pin protocol version at startup (sanity log + fail-fast if the const drifts).
app.Logger.LogInformation("MCP server starting; pinned protocol version: {Version}",
    PinnedProtocolVersion.Value);

// Health probes (T095) — must be reachable without MCP handshake for k8s.
app.MapHealthEndpoints();

// MCP Streamable HTTP endpoint mounted at "/" — clients POST JSON-RPC here.
app.MapMcp();

await app.RunAsync().ConfigureAwait(false);

namespace McpServer
{
    /// <summary>
    /// Marker type so <c>WebApplicationFactory&lt;Program&gt;</c> can locate the
    /// entrypoint assembly from test projects.
    /// </summary>
    public partial class Program;
}
