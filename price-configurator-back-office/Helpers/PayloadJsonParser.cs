using System.Text.Json;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Helpers;

public static class PayloadJsonParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ConfigV1Payload? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
        {
            var sections = JsonSerializer.Deserialize<List<ConfiguratorSectionDto>>(json, Options);
            return sections is null ? null : new ConfigV1Payload(sections);
        }

        return JsonSerializer.Deserialize<ConfigV1Payload>(json, Options);
    }

    public static string Serialize(ConfigV1Payload payload) =>
        JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });
}
