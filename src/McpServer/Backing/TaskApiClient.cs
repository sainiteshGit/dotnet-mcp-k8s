using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpServer.Backing;

/// <summary>
/// Typed HttpClient that talks to <c>/api/v1/tasks</c> on the WebApp.
/// Registered via <c>AddTaskApiClient</c> which also wires Polly resilience
/// and the <see cref="Pipeline.CorrelationIdHandler"/>.
/// </summary>
public sealed class TaskApiClient(HttpClient http) : ITaskApiClient
{
    private const string BasePath = "/api/v1/tasks";
    private readonly JsonSerializerOptions _json = BackingJsonOptions.Default;

    public async Task<BackingResult<TaskItemDto>> CreateAsync(CreateTaskRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await http.PostAsJsonAsync(BasePath, request, _json, ct).ConfigureAwait(false);
        return await ReadAsync<TaskItemDto>(response, ct).ConfigureAwait(false);
    }

    public async Task<BackingResult<TaskListPageDto>> ListAsync(ListTasksQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var url = BuildListUrl(query);
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        return await ReadAsync<TaskListPageDto>(response, ct).ConfigureAwait(false);
    }

    public async Task<BackingResult<TaskItemDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"{BasePath}/{id}", ct).ConfigureAwait(false);
        return await ReadAsync<TaskItemDto>(response, ct).ConfigureAwait(false);
    }

    public async Task<BackingResult<TaskItemDto>> PatchAsync(Guid id, PatchTaskRequestDto patch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{BasePath}/{id}")
        {
            Content = JsonContent.Create(patch, options: _json),
        };
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        return await ReadAsync<TaskItemDto>(response, ct).ConfigureAwait(false);
    }

    public async Task<BackingResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"{BasePath}/{id}", ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new BackingResult<bool>((int)response.StatusCode, true, null);
        }
        var error = await ReadErrorAsync(response, ct).ConfigureAwait(false);
        return new BackingResult<bool>((int)response.StatusCode, false, error);
    }

    internal static string BuildListUrl(ListTasksQuery query)
    {
        var parts = new List<string>(6);
        if (query.Status is { } s)
        {
            parts.Add($"status={Enum2Snake(s)}");
        }
        if (query.Priority is { } p)
        {
            parts.Add($"priority={Enum2Snake(p)}");
        }
        if (query.DueBefore is { } db)
        {
            parts.Add($"due_before={db.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }
        if (query.DueAfter is { } da)
        {
            parts.Add($"due_after={da.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }
        if (query.Page is { } pg)
        {
            parts.Add($"page={pg.ToString(CultureInfo.InvariantCulture)}");
        }
        if (query.PageSize is { } ps)
        {
            parts.Add($"page_size={ps.ToString(CultureInfo.InvariantCulture)}");
        }
        return parts.Count == 0 ? BasePath : $"{BasePath}?{string.Join("&", parts)}";
    }

    private static string Enum2Snake<T>(T value) where T : struct, Enum
    {
        // Lower the PascalCase name and inject underscores before inner caps.
        var raw = value.ToString();
        var sb = new System.Text.StringBuilder(raw.Length + 4);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private async Task<BackingResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(_json, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Backing returned empty success body.");
            return new BackingResult<T>((int)response.StatusCode, value, null);
        }
        var error = await ReadErrorAsync(response, ct).ConfigureAwait(false);
        return new BackingResult<T>((int)response.StatusCode, default, error);
    }

    private async Task<BackingErrorEnvelope> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BackingErrorEnvelope>(_json, ct).ConfigureAwait(false)
                   ?? Fallback(response.StatusCode);
        }
        catch (JsonException)
        {
            return Fallback(response.StatusCode);
        }
        catch (NotSupportedException)
        {
            return Fallback(response.StatusCode);
        }

        static BackingErrorEnvelope Fallback(HttpStatusCode code) =>
            new(new BackingErrorDetails(
                Code: "backend_error",
                Message: $"Backing API returned {(int)code} with no parseable envelope.",
                Details: null));
    }
}
