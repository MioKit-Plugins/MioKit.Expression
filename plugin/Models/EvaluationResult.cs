namespace Expression.Models;

/// <summary>
/// 求值结果
/// </summary>
public record EvaluationResult
{
    /// <summary>
    /// 求值是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 求值结果值
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 格式化后的结果值
    /// </summary>
    public string? FormattedValue { get; init; }

    /// <summary>
    /// 错误信息（求值失败时）
    /// </summary>
    public ExpressionError? Error { get; init; }

    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// 创建成功的求值结果
    /// </summary>
    /// <param name="value">求值结果值</param>
    /// <param name="formatted">格式化后的值（可选）</param>
    /// <param name="executionTime">执行耗时（可选）</param>
    /// <returns>成功的求值结果</returns>
    public static EvaluationResult Ok(object? value, string? formatted = null, TimeSpan executionTime = default)
    {
        return new EvaluationResult
        {
            Success = true,
            Value = value,
            FormattedValue = formatted ?? value?.ToString(),
            ExecutionTime = executionTime
        };
    }

    /// <summary>
    /// 创建失败的求值结果
    /// </summary>
    /// <param name="error">错误信息</param>
    /// <param name="executionTime">执行耗时（可选）</param>
    /// <returns>失败的求值结果</returns>
    public static EvaluationResult Fail(ExpressionError error, TimeSpan executionTime = default)
    {
        return new EvaluationResult
        {
            Success = false,
            Error = error,
            ExecutionTime = executionTime
        };
    }
}
