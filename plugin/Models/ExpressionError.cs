namespace Expression.Models;

/// <summary>
/// 表达式错误
/// </summary>
public record ExpressionError
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 错误上下文信息
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// 内部异常
    /// </summary>
    public Exception? InnerException { get; init; }
}

/// <summary>
/// 错误代码常量
/// </summary>
public static class ErrorCodes
{
    // 解析错误
    public const string EmptyInput = "PARSE_EMPTY_INPUT";
    public const string InvalidSyntax = "PARSE_INVALID_SYNTAX";
    public const string UnknownFunction = "PARSE_UNKNOWN_FUNCTION";

    // 求值错误
    public const string EvaluationFailed = "EVAL_FAILED";
    public const string Overflow = "EVAL_OVERFLOW";
    public const string DivisionByZero = "EVAL_DIV_ZERO";

    // 脚本错误
    public const string ScriptNotRunning = "SCRIPT_NOT_RUNNING";
    public const string FunctionNotFound = "SCRIPT_FUNC_NOT_FOUND";
    public const string ScriptExecutionFailed = "SCRIPT_EXEC_FAILED";
}
