using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PriceConfiguratorBackoffice.Helpers;

/// <summary>
/// Reads and writes known keys inside <see cref="Models.ConfiguratorCard.CardDataJson"/>
/// while preserving any other payload properties (e.g. <c>prices</c>, <c>colors</c>).
/// </summary>
public static class CardPayloadHelper
{
    public static readonly IReadOnlySet<string> ManagedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "price",
        "amount",
        "type",
        "units",
        "appliances",
        "sinks",
        "images",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static decimal? GetDecimal(string? cardDataJson, string key)
    {
        if (!TryGetProperty(cardDataJson, key, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDecimal(out var d) ? d : null,
            JsonValueKind.String when decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => null,
        };
    }

    public static int? GetInt(string? cardDataJson, string key)
    {
        if (!TryGetProperty(cardDataJson, key, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => null,
        };
    }

    public static string? GetString(string? cardDataJson, string key)
    {
        if (!TryGetProperty(cardDataJson, key, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
    }

    public static string NormalizeKeyValueListJson(string? json) =>
        NormalizeKeyValueListJsonDetailed(json).Json;

    /// <summary>
    /// One Cloudinary path per sink option, in the same order as the Sinks list.
    /// Legacy nested arrays <c>[[a,b]]</c> are flattened on read.
    /// </summary>
    public static string NormalizeImagesJson(string? json, string? sinksJson = null)
    {
        var flat = ParseImagesToFlatList(json);
        var sinks = ParseSinkOptionsInOrder(sinksJson);

        if (sinks.Count > 0)
        {
            flat = AlignPathsToSinkCount(flat, sinks.Count);
        }
        else
        {
            flat = flat.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        }

        return flat.Count == 0 ? "[]" : JsonSerializer.Serialize(flat, JsonOptions);
    }

    public static IReadOnlyList<string> ParseImagesToFlatList(string? json)
    {
        if (IsEmptyArray(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            if (root.GetArrayLength() == 0)
            {
                return [];
            }

            if (root[0].ValueKind == JsonValueKind.String)
            {
                return root.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .ToList();
            }

            var flattened = new List<string>();
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var path in item.EnumerateArray())
                {
                    if (path.ValueKind == JsonValueKind.String)
                    {
                        flattened.Add(path.GetString() ?? string.Empty);
                    }
                }
            }

            return flattened;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> AlignPathsToSinkCount(IReadOnlyList<string> paths, int sinkCount)
    {
        var aligned = new List<string>(sinkCount);
        for (var i = 0; i < sinkCount; i++)
        {
            aligned.Add(i < paths.Count ? paths[i].Trim() : string.Empty);
        }

        while (aligned.Count > 0 && string.IsNullOrWhiteSpace(aligned[^1]))
        {
            aligned.RemoveAt(aligned.Count - 1);
        }

        return aligned;
    }

  /// <summary>Sink options in configured list order (same order as the Sinks editor).</summary>
    private static List<CardKeyValueOption> ParseSinkOptionsInOrder(string? sinksJson)
    {
        if (IsEmptyArray(sinksJson))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<CardKeyValueOption>>(sinksJson!, JsonOptions) ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string FormatJsonArray(string? cardDataJson, string key, string emptyDefault = "[]")
    {
        if (!TryGetProperty(cardDataJson, key, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return emptyDefault;
        }

        return JsonSerializer.Serialize(el, JsonOptions);
    }

    public static void HydrateScalars(
        string? cardDataJson,
        ref decimal? price,
        ref decimal? amount,
        ref string? type,
        ref int? units,
        ref string appliancesJson,
        ref string sinksJson,
        ref string imagesJson)
    {
        if (string.IsNullOrWhiteSpace(cardDataJson) || cardDataJson == "{}")
        {
            return;
        }

        price ??= GetDecimal(cardDataJson, "price");
        amount ??= GetDecimal(cardDataJson, "amount");
        type ??= GetString(cardDataJson, "type");
        units ??= GetInt(cardDataJson, "units");

        if (IsEmptyArray(appliancesJson))
        {
            appliancesJson = FormatJsonArray(cardDataJson, "appliances");
        }

        if (IsEmptyArray(sinksJson))
        {
            sinksJson = FormatJsonArray(cardDataJson, "sinks");
        }

        if (IsEmptyArray(imagesJson))
        {
            imagesJson = FormatJsonArray(cardDataJson, "images");
        }
    }

    /// <summary>Result of normalizing a key/value option list (appliances, sinks).</summary>
    public sealed record KeyValueListNormalizeResult(string Json, IReadOnlyList<int> DuplicateKeys);

    public static KeyValueListNormalizeResult NormalizeKeyValueListJsonDetailed(string? json)
    {
        if (IsEmptyArray(json))
        {
            return new KeyValueListNormalizeResult("[]", []);
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<CardKeyValueOption>>(json!, JsonOptions) ?? [];
            var filtered = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToList();

            var duplicateKeys = filtered
                .GroupBy(item => item.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(k => k)
                .ToList();

            var normalized = filtered.Count == 0
                ? "[]"
                : JsonSerializer.Serialize(filtered, JsonOptions);
            return new KeyValueListNormalizeResult(normalized, duplicateKeys);
        }
        catch (JsonException)
        {
            return new KeyValueListNormalizeResult("[]", []);
        }
    }

    public static string MergeIntoCardDataJson(
        string? cardDataJson,
        decimal? price,
        decimal? amount,
        string? type,
        int? units,
        string? appliancesJson,
        string? sinksJson,
        string? imagesJson)
    {
        if (!TryParseObject(cardDataJson, out var root, out var previous))
        {
            return string.IsNullOrWhiteSpace(cardDataJson) ? "{}" : cardDataJson;
        }

        foreach (var key in ManagedKeys)
        {
            root.Remove(key);
        }

        if (price is not null)
        {
            root["price"] = price.Value;
        }

        if (amount is not null)
        {
            root["amount"] = amount.Value;
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            root["type"] = type.Trim();
        }

        if (units is not null)
        {
            root["units"] = units.Value;
        }

        SetJsonProperty(root, previous, "appliances", NormalizeKeyValueListJson(appliancesJson));
        SetJsonProperty(root, previous, "sinks", NormalizeKeyValueListJson(sinksJson));
        SetJsonProperty(root, previous, "images", NormalizeImagesJson(imagesJson, sinksJson));

        return root.Count == 0 ? "{}" : root.ToJsonString(JsonOptions);
    }

    private static bool TryGetProperty(string? cardDataJson, string key, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(cardDataJson) || cardDataJson == "{}")
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(cardDataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    element = prop.Value.Clone();
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryParseObject(string? cardDataJson, out JsonObject root, out JsonObject previous)
    {
        root = new JsonObject();
        previous = new JsonObject();

        if (string.IsNullOrWhiteSpace(cardDataJson) || cardDataJson == "{}")
        {
            return true;
        }

        try
        {
            if (JsonNode.Parse(cardDataJson) is not JsonObject obj)
            {
                return false;
            }

            root = obj;
            previous = (JsonObject)obj.DeepClone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void SetJsonProperty(JsonObject root, JsonObject previous, string key, string? json)
    {
        if (IsEmptyArray(json))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node is not null)
                {
                    root[key] = node;
                    return;
                }
            }
            catch (JsonException)
            {
                // Invalid editor JSON: keep previous value if any.
            }
        }

        if (previous.TryGetPropertyValue(key, out var existing) && existing is not null)
        {
            root[key] = existing.DeepClone();
        }
    }

    public static bool IsEmptyArrayJson(string? json) => IsEmptyArray(json);

    private static bool IsEmptyArray(string? json) =>
        string.IsNullOrWhiteSpace(json)
        || json.Trim() == "[]"
        || json.Trim() == "null";
}
