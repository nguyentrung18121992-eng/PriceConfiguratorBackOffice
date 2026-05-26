using System.Text.Json.Serialization;

namespace PriceConfiguratorBackoffice.Helpers;

public sealed class ConfiguratorMessageEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
