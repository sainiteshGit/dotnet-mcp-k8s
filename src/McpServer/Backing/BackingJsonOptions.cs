using System.Text.Json;
using System.Text.Json.Serialization;

namespace McpServer.Backing;

/// <summary>
/// JSON options shared by the typed <see cref="TaskApiClient"/> and tests:
/// snake-case naming policy + snake-case enum converter so DTOs serialize
/// consistently with <c>contracts/webapp-openapi.yaml</c> on the wire.
/// </summary>
public static class BackingJsonOptions
{
    public static readonly JsonSerializerOptions Default = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
