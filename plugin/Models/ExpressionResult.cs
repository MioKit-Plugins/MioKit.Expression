namespace Expression.Models;

/// <summary>
/// 表达式计算结果（向后兼容类型）
/// </summary>
public record ExpressionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 原始表达式
    /// </summary>
    public string Expression { get; init; } = string.Empty;

    /// <summary>
    /// 计算结果值
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ExpressionResult Ok(string expression, object? value)
    {
        return new ExpressionResult
        {
            Success = true,
            Expression = expression,
            Value = value
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ExpressionResult Fail(string expression, string errorMessage)
    {
        return new ExpressionResult
        {
            Success = false,
            Expression = expression,
            ErrorMessage = errorMessage
        };
    }
}
