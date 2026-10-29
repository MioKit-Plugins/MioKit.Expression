namespace Expression.Models;

/// <summary>
/// 参数类型枚举
/// </summary>
public enum ParameterType
{
    /// <summary>
    /// 数字类型
    /// </summary>
    Number,

    /// <summary>
    /// 字符串类型
    /// </summary>
    String,

    /// <summary>
    /// 占位符类型（如 [clipboard]）
    /// </summary>
    Placeholder,

    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown
}

/// <summary>
/// 参数值
/// </summary>
public record ParameterValue
{
    /// <summary>
    /// 参数值
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 参数类型
    /// </summary>
    public ParameterType Type { get; init; }

    /// <summary>
    /// 原始文本
    /// </summary>
    public required string RawText { get; init; }
}
