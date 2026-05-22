using System.Text.Json.Serialization;

namespace PriceConfiguratorBackoffice.Helpers;

/// <summary>Option row used in card payloads (e.g. appliances, sinks).</summary>
public sealed class CardKeyValueOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public int Key { get; set; }
}
