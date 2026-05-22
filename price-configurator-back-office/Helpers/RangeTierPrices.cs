using System.Text.Json;

namespace PriceConfiguratorBackoffice.Helpers;

/// <summary>
/// Unit-tier price arrays stored as JSON [small, medium, large, xlarge] on <see cref="Models.RangePriceSet"/>.
/// </summary>
public static class RangeTierPrices
{
    public static readonly string[] TierOrder = ["small", "medium", "large", "xlarge"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static decimal? GetTier(string? json, int index)
    {
        var values = DeserializeArray(json);
        if (values is null || index < 0 || index >= values.Length)
        {
            return null;
        }

        return values[index];
    }

    public static string ToJsonArray(params decimal?[] tiers)
    {
        var values = new decimal[TierOrder.Length];
        for (var i = 0; i < TierOrder.Length; i++)
        {
            values[i] = i < tiers.Length && tiers[i].HasValue ? tiers[i]!.Value : 0m;
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    public static string? ToJsonArrayOrNull(params decimal?[] tiers)
    {
        if (tiers.Length == 0 || tiers.All(t => t is null))
        {
            return null;
        }

        return ToJsonArray(tiers);
    }

    public static Dictionary<string, decimal>? ToDictionary(string? json)
    {
        var values = DeserializeArray(json);
        if (values is null || values.Length == 0)
        {
            return null;
        }

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < TierOrder.Length && i < values.Length; i++)
        {
            result[TierOrder[i]] = values[i];
        }

        return result.Count == 0 ? null : result;
    }

    public static string FromDictionary(object? tierPrices)
    {
        if (tierPrices is null)
        {
            return "[]";
        }

        var element = tierPrices is JsonElement json
            ? json
            : JsonSerializer.SerializeToElement(tierPrices, JsonOptions);

        if (element.ValueKind != JsonValueKind.Object)
        {
            return "[]";
        }

        var tiers = new decimal?[TierOrder.Length];
        for (var i = 0; i < TierOrder.Length; i++)
        {
            if (element.TryGetProperty(TierOrder[i], out var prop) && prop.TryGetDecimal(out var value))
            {
                tiers[i] = value;
            }
        }

        return ToJsonArray(tiers);
    }

    private static decimal[]? DeserializeArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return null;
        }

        return JsonSerializer.Deserialize<decimal[]>(json, JsonOptions);
    }
}
