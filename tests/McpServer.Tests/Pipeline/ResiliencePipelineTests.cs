using System.Diagnostics;
using FluentAssertions;
using McpServer.Backing;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpServer.Tests.Pipeline;

/// <summary>
/// T073 — Resilience pipeline contract (research.md §2, SC-009).
///
/// Asserts that calls made via the typed <see cref="ITaskApiClient"/> obtained
/// from <see cref="ServiceCollectionExtensions.AddTaskApiClient"/>:
/// <list type="bullet">
///   <item>complete WITHIN the 5-second total-budget when the upstream is unreachable;</item>
///   <item>retry transient 5xx responses (at least one retry observed);</item>
///   <item>do NOT mask successful responses (happy path still succeeds).</item>
/// </list>
/// </summary>
public class ResiliencePipelineTests : IAsyncLifetime
{
    private WireMockServer _wiremock = null!;

    public Task InitializeAsync()
    {
        _wiremock = WireMockServer.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _wiremock.Stop();
        _wiremock.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pipeline_returns_within_5_second_budget_when_upstream_unreachable()
    {
        // Point the client at a port that is definitely not listening.
        var unreachable = new Uri("http://127.0.0.1:1");
        await using var sp = new ServiceCollection().AddTaskApiClient(unreachable).BuildServiceProvider();
        var client = sp.GetRequiredService<ITaskApiClient>();

        var sw = Stopwatch.StartNew();
        Func<Task> act = async () => await client.GetAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>("upstream unreachable should bubble up an exception for the tool layer to translate");
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6),
            "the total-budget timeout strategy must keep callers within 5s + small overhead");
    }

    [Fact]
    public async Task Pipeline_retries_transient_500_and_eventually_succeeds()
    {
        var id = Guid.NewGuid();
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{id}").UsingGet())
                 .InScenario("retry")
                 .WillSetStateTo("after-first-500")
                 .RespondWith(Response.Create().WithStatusCode(500));
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{id}").UsingGet())
                 .InScenario("retry")
                 .WhenStateIs("after-first-500")
                 .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                     .WithBody($$"""
                     {"id":"{{id}}","title":"t","description":null,"status":"todo","priority":"medium","due_date":null,"created_at":"2026-05-16T10:00:00Z","updated_at":"2026-05-16T10:00:00Z"}
                     """));

        await using var sp = new ServiceCollection().AddTaskApiClient(new Uri(_wiremock.Url!)).BuildServiceProvider();
        var client = sp.GetRequiredService<ITaskApiClient>();

        var result = await client.GetAsync(id);

        result.IsSuccess.Should().BeTrue("retry strategy should turn the second 200 into success");
        _wiremock.LogEntries.Should().HaveCountGreaterThanOrEqualTo(2, "at least one retry was issued");
    }

    [Fact]
    public async Task Pipeline_does_not_interfere_with_happy_path()
    {
        var id = Guid.NewGuid();
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{id}").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                     .WithBody($$"""
                     {"id":"{{id}}","title":"t","description":null,"status":"todo","priority":"medium","due_date":null,"created_at":"2026-05-16T10:00:00Z","updated_at":"2026-05-16T10:00:00Z"}
                     """));

        await using var sp = new ServiceCollection().AddTaskApiClient(new Uri(_wiremock.Url!)).BuildServiceProvider();
        var client = sp.GetRequiredService<ITaskApiClient>();

        var result = await client.GetAsync(id);

        result.IsSuccess.Should().BeTrue();
        _wiremock.LogEntries.Should().ContainSingle("happy path should not trigger retries");
    }
}
