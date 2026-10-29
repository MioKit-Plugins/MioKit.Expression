namespace Expression.Models;

public class CompileResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string[] Functions { get; set; } = [];
}

public class ApplyResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CalculateResult
{
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class ExpressionSettingsDto
{
    public int DecimalPrecision { get; set; }
    public bool UseThousandsSeparator { get; set; }
    public bool ScriptEnabled { get; set; }
}

public sealed class ExpressionLogEventArgs : EventArgs
{
    public string Level { get; init; } = "info";
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
