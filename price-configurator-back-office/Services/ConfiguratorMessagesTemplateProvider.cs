using System.Collections.Concurrent;
using System.Text.Json;
using Nobia.CmsToolkit.Options;
using PriceConfiguratorBackoffice.Helpers;

namespace PriceConfiguratorBackoffice.Services;

/// <summary>
/// Fixed message keys and default values per brand/language from seed files.
/// </summary>
public sealed class ConfiguratorMessagesTemplateProvider(IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache = new();

    public IReadOnlyDictionary<string, string> GetTemplate(string brand, string? language)
    {
        var normalizedBrand = LanguageMapper.NormalizeBrand(brand);
        var seedLanguage = ResolveSeedLanguage(normalizedBrand, language);
        var cacheKey = $"{normalizedBrand}|{seedLanguage}";

        return _cache.GetOrAdd(cacheKey, _ => LoadTemplate(normalizedBrand, seedLanguage));
    }

    private static string ResolveSeedLanguage(string brand, string? language)
    {
        if (!string.IsNullOrWhiteSpace(language) && language.Contains('-', StringComparison.Ordinal))
        {
            return language.Trim();
        }

        var contentLanguage = string.IsNullOrWhiteSpace(language)
            ? ContentLanguage.en
            : LanguageMapper.ToContentLanguage(brand, language);

        return LanguageMapper.ToApiLanguageCode(brand, contentLanguage);
    }

    public string GetTemplateJson(string brand, string? language) =>
        JsonSerializer.Serialize(GetTemplate(brand, language), JsonOptions);

    private IReadOnlyDictionary<string, string> LoadTemplate(string brand, string language)
    {
        var seedsDir = Path.Combine(env.ContentRootPath, "Data", "Seeds");

        var messagesPath = Path.Combine(seedsDir, $"{brand}-{language}.messages.json");
        if (File.Exists(messagesPath))
        {
            return DeserializeDictionary(File.ReadAllText(messagesPath));
        }

        var payloadPath = Path.Combine(seedsDir, $"{brand}-{language}.payload.json");
        if (File.Exists(payloadPath))
        {
            var fromPayload = TryReadMessagesFromPayload(payloadPath);
            if (fromPayload.Count > 0)
            {
                return fromPayload;
            }
        }

        var fallbackPayload = Path.Combine(seedsDir, "magnet-en-GB.payload.json");
        if (File.Exists(fallbackPayload))
        {
            return TryReadMessagesFromPayload(fallbackPayload);
        }

        return new Dictionary<string, string>();
    }

    private static Dictionary<string, string> TryReadMessagesFromPayload(string payloadPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(payloadPath));
            if (!doc.RootElement.TryGetProperty("messages", out var messages)
                || messages.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return messages.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, string> DeserializeDictionary(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
        ?? new Dictionary<string, string>();
}
