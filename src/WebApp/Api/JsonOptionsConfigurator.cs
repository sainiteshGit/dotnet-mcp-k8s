using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApp.Api;

/// <summary>
/// Centralises the JSON conventions for the public Task Manager Web API (T039):
/// snake_case property names, snake_case string enums, and tolerant
/// deserialisation (unknown fields ignored). Matches the OpenAPI contract in
/// <c>specs/001-task-manager-api/contracts/webapp-openapi.yaml</c> where
/// fields are <c>page_size</c>, <c>due_before</c>, <c>created_at</c>, etc.
/// </summary>
public static class JsonOptionsConfigurator
{
    public static void ConfigureSnakeCase(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PropertyNameCaseInsensitive = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }
}
