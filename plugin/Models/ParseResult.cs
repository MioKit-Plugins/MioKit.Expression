namespace Expression.Models;

/// <summary>
/// 表达式解析结果
/// </summary>
public record ParseResult
{
    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 解析后的表达式
    /// </summary>
    public string? Expression { get; init; }

    /// <summary>
    /// 函数名（如果是函数调用）
    /// </summary>
    public string? FunctionName { get; init; }

    /// <summary>
    /// 函数参数列表
    /// </summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>
    /// 错误代码（解析失败时）
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息（解析失败时）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建成功的解析结果
    /// </summary>
    /// <param name="expression">解析后的表达式</param>
    /// <param name="funcName">函数名（可选）</param>
    /// <param name="args">参数列表（可选）</param>
    /// <returns>成功的解析结果</returns>
    public static ParseResult Ok(string expression, string? funcName = null, IReadOnlyList<string>? args = null)
    {
        return new ParseResult
        {
            Success = true,
            Expression = expression,
            FunctionName = funcName,
            Arguments = args
        };
    }

    /// <summary>
    /// 创建失败的解析结果
    /// </summary>
    /// <param name="errorCode">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <returns>失败的解析结果</returns>
    public static ParseResult Fail(string errorCode, string message)
    {
        return new ParseResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message
        };
    }
}
