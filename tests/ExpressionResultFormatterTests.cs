using System.Globalization;
using Expression.Services;
using Xunit;

namespace Expression.Tests;

public sealed class ExpressionResultFormatterTests
{
    [Theory]
    [InlineData("1.6", 0, "2")]
    [InlineData("-1.6", 0, "-2")]
    [InlineData("2.5", 0, "2")]
    [InlineData("1.234567", 3, "1.235")]
    [InlineData("1.1234567890123456789012345678", 28, "1.1234567890123456789012345678")]
    public void DecimalHonorsPrecisionIncludingZeroAndTwentyEight(string input, int digits, string expected)
    {
        Assert.Equal(expected, Format(decimal.Parse(input, CultureInfo.InvariantCulture), digits));
    }

    [Theory]
    [InlineData(0, "2")]
    [InlineData(15, "1.6")]
    [InlineData(16, "1.6")]
    [InlineData(28, "1.6")]
    public void DoubleNeverExceedsNativeRoundingLimit(int digits, string expected)
    {
        Assert.Equal(expected, Format(1.6d, digits));
        Assert.Equal(expected, Format(1.6f, digits));
    }

    [Fact]
    public void NumberKindsShareGroupingCultureAndNonNumericFormatting()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal("1.234,50", ExpressionResultFormatter.Format(1234.5m, 2, true, culture));
        Assert.Equal("1.234,50", ExpressionResultFormatter.Format(1234.5d, 2, true, culture));
        Assert.Equal("1.234", ExpressionResultFormatter.Format(1234, 2, true, culture));
        Assert.Equal("1234.5", ExpressionResultFormatter.Format(1234.5m, 2, false, culture));
        Assert.Equal("1234.5", ExpressionResultFormatter.Format(1234.5d, 2, false, culture));
        Assert.Equal("null", Format(null, 0));
        Assert.Equal("true", Format(true, 0));
        Assert.Equal("hello", Format("hello", 0));
        Assert.Equal("0", Format(-0.1d, 0));
        Assert.Contains("1E+100", Format(1e100, 28));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public void InvalidPrecisionIsRejected(int digits) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Format(1m, digits));

    internal static string Format(object? value, int precision = 10) =>
        ExpressionResultFormatter.Format(value, precision, false, CultureInfo.InvariantCulture);
}
