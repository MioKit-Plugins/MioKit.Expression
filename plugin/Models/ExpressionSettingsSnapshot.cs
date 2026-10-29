namespace Expression.Models;

public sealed record ExpressionSettingsSnapshot
{
    public bool ScriptEnabled { get; init; } = ExpressionSettings.Defaults.ScriptEnabled;
    public string ScriptContent { get; init; } = ExpressionSettings.Defaults.ScriptContent;
    public int DecimalPrecision { get; init; } = ExpressionSettings.Defaults.DecimalPrecision;
    public bool UseThousandsSeparator { get; init; } = ExpressionSettings.Defaults.UseThousandsSeparator;
}
