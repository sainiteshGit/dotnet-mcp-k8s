using FluentAssertions;
using McpServer.Backing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace McpServer.Tests.Backing;

/// <summary>
/// Unit coverage of the typed <see cref="TaskApiClient"/> using a WireMock-backed
/// HTTP server. Each test asserts (a) the outbound HTTP shape — verb, path,
/// query/body — and (b) that the deserialized <see cref="BackingResult{T}"/>
/// matches the wire envelope.
/// </summary>
public class TaskApiClientTests : IAsyncLifetime, IDisposable
{
    private WireMockServer _wiremock = null!;
    private HttpClient _http = null!;
    private TaskApiClient _client = null!;

    public Task InitializeAsync()
    {
        _wiremock = WireMockServer.Start();
        _http = new HttpClient { BaseAddress = new Uri(_wiremock.Url!) };
        _client = new TaskApiClient(_http);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _wiremock.Stop();
        _wiremock.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _http?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_posts_snake_case_body_and_returns_201_dto()
    {
        var id = Guid.NewGuid();
        _wiremock.Given(Request.Create().WithPath("/api/v1/tasks").UsingPost())
                 .RespondWith(Response.Create().WithStatusCode(201).WithHeader("Content-Type", "application/json")
                     .WithBody($$"""
                     {"id":"{{id}}","title":"buy milk","description":null,"status":"todo","priority":"medium","due_date":null,"created_at":"2026-05-16T10:00:00Z","updated_at":"2026-05-16T10:00:00Z"}
                     """));

        var result = await _client.CreateAsync(new CreateTaskRequestDto("buy milk"));

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Value!.Id.Should().Be(id);
        result.Value.Status.Should().Be(TaskStatusDto.Todo);

        var log = _wiremock.LogEntries.Single();
        log.RequestMessage.Body.Should().Contain("\"title\":\"buy milk\"");
        log.RequestMessage.Body.Should().NotContain("description");
    }

    [Fact]
    public async Task CreateAsync_propagates_validation_error_envelope_on_400()
    {
        _wiremock.Given(Request.Create().WithPath("/api/v1/tasks").UsingPost())
                 .RespondWith(Response.Create().WithStatusCode(400).WithHeader("Content-Type", "application/json")
                     .WithBody("""{"error":{"code":"validation_error","message":"title is required"}}"""));

        var result = await _client.CreateAsync(new CreateTaskRequestDto("x"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error!.Error.Code.Should().Be("validation_error");
        result.Error.Error.Message.Should().Be("title is required");
    }

    [Fact]
    public async Task GetAsync_returns_not_found_envelope_for_404()
    {
        var id = Guid.NewGuid();
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{id}").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(404).WithHeader("Content-Type", "application/json")
                     .WithBody("""{"error":{"code":"not_found","message":"no such task"}}"""));

        var result = await _client.GetAsync(id);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task DeleteAsync_returns_true_on_204_and_envelope_on_404()
    {
        var ok = Guid.NewGuid();
        var missing = Guid.NewGuid();

        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{ok}").UsingDelete())
                 .RespondWith(Response.Create().WithStatusCode(204));
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{missing}").UsingDelete())
                 .RespondWith(Response.Create().WithStatusCode(404).WithHeader("Content-Type", "application/json")
                     .WithBody("""{"error":{"code":"not_found","message":"no such task"}}"""));

        var deleted = await _client.DeleteAsync(ok);
        deleted.IsSuccess.Should().BeTrue();
        deleted.Value.Should().BeTrue();

        var nope = await _client.DeleteAsync(missing);
        nope.IsSuccess.Should().BeFalse();
        nope.Error!.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task ListAsync_builds_query_string_with_snake_case_filters_and_pagination()
    {
        _wiremock.Given(Request.Create().WithPath("/api/v1/tasks").UsingGet())
                 .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                     .WithBody("""{"items":[],"page":2,"page_size":10,"total":0}"""));

        var q = new ListTasksQuery(
            Status: TaskStatusDto.InProgress,
            Priority: TaskPriorityDto.High,
            DueBefore: new DateOnly(2026, 6, 1),
            DueAfter: new DateOnly(2026, 5, 1),
            Page: 2,
            PageSize: 10);

        var result = await _client.ListAsync(q);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(0);
        result.Value.Page.Should().Be(2);

        var qs = _wiremock.LogEntries.Single().RequestMessage.RawQuery!;
        qs.Should().Contain("status=in_progress");
        qs.Should().Contain("priority=high");
        qs.Should().Contain("due_before=2026-06-01");
        qs.Should().Contain("due_after=2026-05-01");
        qs.Should().Contain("page=2");
        qs.Should().Contain("page_size=10");
    }

    [Fact]
    public async Task PatchAsync_uses_PATCH_verb_and_serializes_only_set_fields()
    {
        var id = Guid.NewGuid();
        _wiremock.Given(Request.Create().WithPath($"/api/v1/tasks/{id}").UsingPatch())
                 .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                     .WithBody($$"""
                     {"id":"{{id}}","title":"t","description":null,"status":"done","priority":"medium","due_date":null,"created_at":"2026-05-16T10:00:00Z","updated_at":"2026-05-16T11:00:00Z"}
                     """));

        var result = await _client.PatchAsync(id, new PatchTaskRequestDto(Status: TaskStatusDto.Done));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(TaskStatusDto.Done);

        var log = _wiremock.LogEntries.Single();
        log.RequestMessage.Method.Should().Be("PATCH");
        log.RequestMessage.Body.Should().Contain("\"status\":\"done\"");
        log.RequestMessage.Body.Should().NotContain("title");
        log.RequestMessage.Body.Should().NotContain("priority");
    }
}
