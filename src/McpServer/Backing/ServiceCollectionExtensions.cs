using McpServer.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace McpServer.Backing;

/// <summary>
/// Registers the typed <see cref="TaskApiClient"/> with:
/// <list type="bullet">
///   <item><see cref="CorrelationIdHandler"/> — stamps every outbound request with <c>X-Correlation-Id</c>.</item>
///   <item>Standard Polly v8 resilience strategy — total timeout, retry with jitter, circuit breaker.</item>
///   <item>Hard 5-second total budget per call (SC-009, research.md §2).</item>
/// </list>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The MCP→WebApp resilience budget enforced by the total-timeout strategy.</summary>
    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddTaskApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        services.AddTransient<CorrelationIdHandler>();

        services.AddHttpClient<ITaskApiClient, TaskApiClient>(http =>
        {
            http.BaseAddress = baseAddress;
            http.Timeout = Timeout.InfiniteTimeSpan; // resilience pipeline owns the timeout.
        })
        .AddHttpMessageHandler<CorrelationIdHandler>()
        .AddResilienceHandler("mcp-to-webapp", builder =>
        {
            // Hard total budget — wraps all attempts.
            builder.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TotalRequestTimeout,
                Name = "total-budget",
            });

            // Bounded retry with jitter for transient failures.
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(100),
                Name = "transient-retry",
            });

            // Per-attempt timeout < total budget so retries can still happen.
            builder.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(2),
                Name = "per-attempt",
            });

            // Circuit breaker trips after sustained failures to protect upstream.
            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                Name = "upstream-breaker",
            });
        });

        return services;
    }
}
