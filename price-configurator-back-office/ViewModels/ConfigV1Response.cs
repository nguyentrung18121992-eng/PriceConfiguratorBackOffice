using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PriceConfiguratorBackoffice.ViewModels;

public record ConfigV1Response(
    [property: Required] int Version,
    [property: Required] DateTime PublishedAt,
    [property: Required] string Brand,
    [property: Required] string Language,
    [property: Required] IReadOnlyList<ConfiguratorSectionDto> Sections,
    IReadOnlyDictionary<string, string>? Messages = null,
    bool Preview = false);

public record ConfigV1Payload(
    [property: Required] IReadOnlyList<ConfiguratorSectionDto> Sections,
    IReadOnlyDictionary<string, string>? Messages = null);

public class ConfiguratorSectionDto
{
    public string Id { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? ShortTitle { get; set; }
    public string? Description { get; set; }
    public object? Tooltip { get; set; }
    public List<ConfiguratorCardDto>? Cards { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}

public class ConfiguratorCardDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Image { get; set; }
    public string? ImageSrc { get; set; }
    public object? Prices { get; set; }
    public object? PrebuiltPrices { get; set; }
    public object? HandlelessPrices { get; set; }
    public decimal? Price { get; set; }
    public int? Units { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
