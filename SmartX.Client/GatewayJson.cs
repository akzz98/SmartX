using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartX.Client;

/// <summary>
/// Matches SmartX.Api JSON (camelCase + string enums) so TelemetryPacket-related
/// DTOs deserialize without treating "critical" as an integer.
/// </summary>
public static class GatewayJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
