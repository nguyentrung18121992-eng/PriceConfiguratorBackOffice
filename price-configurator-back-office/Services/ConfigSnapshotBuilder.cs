using System.Text.Json;
using Nobia.CmsToolkit.Entity;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;
using PriceConfiguratorBackoffice.Models;
using PriceConfiguratorBackoffice.ViewModels;

namespace PriceConfiguratorBackoffice.Services;

public class ConfigSnapshotBuilder(IEntityLoader loader, IWebHostEnvironment env, ILogger<ConfigSnapshotBuilder> logger)
    : IConfigSnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly string[] RangePriceCardKeys =
    [
        "prices",
        "prebuiltPrices",
        "handlelessPrices",
    ];

    public async Task<ConfigV1Payload?> BuildFromDraftAsync(
        string brand,
        ContentLanguage language,
        CancellationToken cancellationToken = default)
    {
        var sections = await loader.Get<ConfiguratorSection>(brand, language);
        var cards = await loader.Get<ConfiguratorCard>(brand, language);
        var messagesList = await loader.Get<ConfiguratorMessages>(brand, language);

        if (!sections.Any() && !cards.Any())
        {
            return null;
        }

        var rangePriceSets = await loader.Get<RangePriceSet>(brand, language);
        var rangePricesByKey = rangePriceSets.ToDictionary(r => r.RangeKey, StringComparer.OrdinalIgnoreCase);

        var settingsList = await loader.Get<ConfiguratorSettings>(brand, language);
        var sectionOrder = ParseSectionOrder(settingsList.FirstOrDefault()?.SectionOrder);

        var sectionDtos = sections
            .OrderBy(s => IndexOf(sectionOrder, s.SectionId, s.SortOrder))
            .Select(section => MapSection(
                section,
                cards.Where(c => c.SectionId == section.SectionId),
                IsRangeSection(section) ? rangePricesByKey : null))
            .ToList();

        var messages = ParseMessagesJson(messagesList.FirstOrDefault()?.MessagesJson);

        return new ConfigV1Payload(sectionDtos, messages);
    }

    public ConfigV1Payload? BuildFromSeedFile(string brand, string language)
    {
        var payloadPath = Path.Combine(env.ContentRootPath, "Data", "Seeds", $"{brand}-{language}.payload.json");
        if (File.Exists(payloadPath))
        {
            return PayloadJsonParser.Parse(File.ReadAllText(payloadPath));
        }

        var sectionsPath = Path.Combine(env.ContentRootPath, "Data", "Seeds", $"{brand}-{language}.sections.json");
        if (!File.Exists(sectionsPath))
        {
            sectionsPath = Path.Combine(env.ContentRootPath, "Data", "Seeds", $"{brand}-en-GB.sections.json");
        }

        if (!File.Exists(sectionsPath))
        {
            logger.LogWarning("Seed file not found for {Brand} {Language}", brand, language);
            return null;
        }

        var sections = JsonSerializer.Deserialize<List<ConfiguratorSectionDto>>(File.ReadAllText(sectionsPath), JsonOptions);
        if (sections is null)
        {
            return null;
        }

        var messagesPath = Path.Combine(env.ContentRootPath, "Data", "Seeds", $"{brand}-{language}.messages.json");
        Dictionary<string, string>? messages = null;
        if (File.Exists(messagesPath))
        {
            messages = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(messagesPath), JsonOptions);
        }

        return new ConfigV1Payload(sections, messages);
    }

    private static Dictionary<string, string>? ParseMessagesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
    }

    private static bool IsRangeSection(ConfiguratorSection section) =>
        string.Equals(section.Type, "range", StringComparison.OrdinalIgnoreCase)
        || string.Equals(section.SectionId, "range", StringComparison.OrdinalIgnoreCase);

    private static ConfiguratorSectionDto MapSection(
        ConfiguratorSection section,
        IEnumerable<ConfiguratorCard> cards,
        IReadOnlyDictionary<string, RangePriceSet>? rangePricesByKey)
    {
        var dto = new ConfiguratorSectionDto
        {
            Id = section.SectionId,
            Type = section.Type,
            Title = section.Name ?? section.Title,
            ShortTitle = section.ShortTitle,
            Description = section.Description,
        };

        if (!string.IsNullOrWhiteSpace(section.TooltipTitle) || section.TooltipDescription.Count > 0)
        {
            dto.Tooltip = new
            {
                title = section.TooltipTitle,
                description = section.TooltipDescription,
            };
        }

        dto.Cards = cards
            .OrderBy(c => c.SortOrder)
            .Select(c => MapCard(c, rangePricesByKey))
            .ToList();

        return dto;
    }

    private static ConfiguratorCardDto MapCard(
        ConfiguratorCard card,
        IReadOnlyDictionary<string, RangePriceSet>? rangePricesByKey)
    {
        var dto = new ConfiguratorCardDto
        {
            Title = card.Name ?? card.Title,
            Description = card.Description,
            Image = card.Image?.PublicId,
        };

        if (!string.IsNullOrWhiteSpace(card.CardDataJson) && card.CardDataJson != "{}")
        {
            var extra = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(card.CardDataJson, JsonOptions);
            if (extra is not null)
            {
                dto.ExtensionData = extra.ToDictionary(
                    k => k.Key,
                    v => (object?)JsonSerializer.Deserialize<object>(v.Value.GetRawText(), JsonOptions));
            }
        }

        if (rangePricesByKey is not null
            && rangePricesByKey.TryGetValue(card.CardKey, out var rangeSet))
        {
            dto.Prices = RangeTierPrices.ToDictionary(rangeSet.PricesJson);
            dto.PrebuiltPrices = RangeTierPrices.ToDictionary(rangeSet.PrebuiltPricesJson);
            dto.HandlelessPrices = RangeTierPrices.ToDictionary(rangeSet.HandlelessPricesJson);
            RemoveRangePriceKeysFromExtensionData(dto);
        }

        return dto;
    }

    private static void RemoveRangePriceKeysFromExtensionData(ConfiguratorCardDto dto)
    {
        if (dto.ExtensionData is null)
        {
            return;
        }

        foreach (var key in RangePriceCardKeys)
        {
            dto.ExtensionData.Remove(key);
        }

        if (dto.ExtensionData.Count == 0)
        {
            dto.ExtensionData = null;
        }
    }

    private static IReadOnlyList<string> ParseSectionOrder(string? sectionOrder)
    {
        if (string.IsNullOrWhiteSpace(sectionOrder))
        {
            return [];
        }

        return sectionOrder.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int IndexOf(IReadOnlyList<string> order, string sectionId, int sortOrder)
    {
        var index = order.Count > 0 ? Array.IndexOf(order.ToArray(), sectionId) : -1;
        return index >= 0 ? index : sortOrder + 1000;
    }
}
