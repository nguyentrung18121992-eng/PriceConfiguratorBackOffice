using System.Text.Json;
using PriceConfiguratorBackoffice.Services;

namespace PriceConfiguratorBackoffice.Helpers;

public static class ConfiguratorMessagesHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string BuildEntriesJson(
        string brand,
        string? language,
        string? messagesJson,
        ConfiguratorMessagesTemplateProvider templates)
    {
        var template = templates.GetTemplate(brand, language);
        var stored = ParseMessagesDictionary(messagesJson);
        var entries = template
            .Select(pair => new ConfiguratorMessageEntry
            {
                Key = pair.Key,
                Value = stored.TryGetValue(pair.Key, out var value) ? value : pair.Value,
            })
            .ToList();

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    public static string NormalizeMessagesJson(
        string? messagesJson,
        IReadOnlyDictionary<string, string> template)
    {
        var stored = ParseMessagesDictionary(messagesJson);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in template.Keys)
        {
            merged[key] = stored.TryGetValue(key, out var value) ? value : template[key];
        }

        return merged.Count == 0 ? "{}" : JsonSerializer.Serialize(merged, JsonOptions);
    }

    public static string MergeEntriesToMessagesJson(
        string? entriesJson,
        IReadOnlyDictionary<string, string> template)
    {
        var valuesByKey = ParseEntries(entriesJson)
            .Where(e => !string.IsNullOrWhiteSpace(e.Key))
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value ?? string.Empty, StringComparer.Ordinal);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in template.Keys)
        {
            if (valuesByKey.TryGetValue(key, out var value))
            {
                merged[key] = value;
            }
            else
            {
                merged[key] = template[key];
            }
        }

        return merged.Count == 0 ? "{}" : JsonSerializer.Serialize(merged, JsonOptions);
    }

    public static Dictionary<string, string> ParseMessagesDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static List<ConfiguratorMessageEntry> ParseEntries(string? entriesJson)
    {
        if (string.IsNullOrWhiteSpace(entriesJson) || entriesJson.Trim() == "[]")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ConfiguratorMessageEntry>>(entriesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
