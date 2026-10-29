using System.Globalization;
using System.Text.Json;

namespace Expression.Services;

internal static class ExpressionResultFormatter
{
    internal static string Format(object? value, int precision, bool thousands, CultureInfo culture)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(precision);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, 28);
        return value switch
        {
            null => "null",
            decimal number => FormatDecimal(number, precision, thousands, culture),
            double number => FormatDouble(number, precision, thousands, culture),
            float number => FormatDouble(number, Math.Min(precision, 7), thousands, culture),
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                ((IFormattable)value).ToString(thousands ? "N0" : "G", thousands ? culture : CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            string text => text,
            _ => Serialize(value)
        };
    }

    private static string FormatDecimal(decimal value, int precision, bool thousands, CultureInfo culture)
    {
        var rounded = Math.Round(value, precision);
        return thousands
            ? rounded.ToString($"N{precision}", culture)
            : rounded.ToString("G29", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double value, int precision, bool thousands, CultureInfo culture)
    {
        // Math.Round(double, digits) accepts at most 15 digits. Decimal values
        // retain the full configured 0-28 range; doubles keep their native limit.
        var digits = Math.Min(precision, 15);
        var rounded = Math.Round(value, digits);
        if (rounded == 0) rounded = 0; // Avoid displaying a rounded negative zero.
        return thousands
            ? rounded.ToString($"N{digits}", culture)
            : rounded.ToString("G15", CultureInfo.InvariantCulture);
    }

    private static string Serialize(object value)
    {
        try { return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }); }
        catch { return value.ToString() ?? string.Empty; }
    }
}
