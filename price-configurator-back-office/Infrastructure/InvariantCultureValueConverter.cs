using System.Globalization;
using System.Reflection;
using Nobia.CmsToolkit.Entity;

namespace PriceConfiguratorBackoffice.Infrastructure;

/// <summary>
/// Parses decimal/double form values with invariant culture (e.g. 1234.50) and falls back to current culture.
/// </summary>
public sealed class InvariantCultureValueConverter : IValueConverter
{
    private static readonly ValueConverter Inner = new();

    public object ConvertValue(PropertyInfo property, string? value)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type == typeof(decimal))
        {
            return ParseDecimal(value, property);
        }

        if (type == typeof(double))
        {
            return ParseDouble(value, property);
        }

        return Inner.ConvertValue(property, value ?? string.Empty);
    }

    private static object? ParseDecimal(string? value, PropertyInfo property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IsNullable(property) ? null : 0m;
        }

        if (TryParseDecimal(value, out var parsed))
        {
            return parsed;
        }

        throw new FormatException(
            $"\"{value}\" is not a valid price. Use a dot for decimals (e.g. 1234.50).");
    }

    private static object? ParseDouble(string? value, PropertyInfo property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IsNullable(property) ? null : 0d;
        }

        if (TryParseDouble(value, out var parsed))
        {
            return parsed;
        }

        throw new FormatException(
            $"\"{value}\" is not a valid number. Use a dot for decimals (e.g. 1234.50).");
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
        || double.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);

    private static bool IsNullable(PropertyInfo property)
    {
        var nullableContext = new NullabilityInfoContext().Create(property);
        return nullableContext.WriteState == NullabilityState.Nullable;
    }
}
