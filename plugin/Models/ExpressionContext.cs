using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Expression.Models;

/// <summary>
/// 表达式上下文（不可变）
/// 包含表达式执行所需的所有上下文信息
/// </summary>
public sealed class ExpressionContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 原始用户输入
    /// </summary>
    public string RawInput { get; }

    /// <summary>
    /// 解析后的表达式
    /// </summary>
    public string ParsedExpression { get; }

    /// <summary>
    /// 参数字典（只读）
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// 剪贴板内容
    /// </summary>
    public string? ClipboardContent { get; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// 私有构造函数，确保只能通过工厂方法创建
    /// </summary>
    private ExpressionContext(
        string rawInput,
        string parsedExpression,
        IReadOnlyDictionary<string, object?> parameters,
        string? clipboardContent,
        DateTime createdAt)
    {
        RawInput = rawInput;
        ParsedExpression = parsedExpression;
        Parameters = parameters;
        ClipboardContent = clipboardContent;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// 创建表达式上下文
    /// </summary>
    /// <param name="rawInput">原始用户输入</param>
    /// <param name="parsedExpression">解析后的表达式</param>
    /// <param name="parameters">参数字典（可选）</param>
    /// <param name="clipboardContent">剪贴板内容（可选）</param>
    /// <returns>不可变的表达式上下文</returns>
    /// <exception cref="ArgumentNullException">当 rawInput 或 parsedExpression 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当 rawInput 或 parsedExpression 为空或仅包含空白字符时抛出</exception>
    public static ExpressionContext Create(
        string rawInput,
        string parsedExpression,
        IDictionary<string, object?>? parameters = null,
        string? clipboardContent = null)
    {
        ArgumentNullException.ThrowIfNull(rawInput);
        ArgumentNullException.ThrowIfNull(parsedExpression);

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            throw new ArgumentException("Raw input cannot be empty or whitespace.", nameof(rawInput));
        }

        if (string.IsNullOrWhiteSpace(parsedExpression))
        {
            throw new ArgumentException("Parsed expression cannot be empty or whitespace.", nameof(parsedExpression));
        }

        // 创建不可变的参数字典
        var immutableParams = parameters != null
            ? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(parameters))
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

        return new ExpressionContext(
            rawInput,
            parsedExpression,
            immutableParams,
            clipboardContent,
            DateTime.UtcNow);
    }

    /// <summary>
    /// 按名称获取参数值
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <returns>参数值，如果不存在则返回 null</returns>
    public object? GetParameter(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Parameters.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// 按索引获取参数值（使用 "arg0", "arg1" 等命名约定）
    /// </summary>
    /// <param name="index">参数索引（从 0 开始）</param>
    /// <returns>参数值，如果不存在则返回 null</returns>
    /// <exception cref="ArgumentOutOfRangeException">当索引为负数时抛出</exception>
    public object? GetParameter(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative.");
        }

        var key = $"arg{index}";
        return Parameters.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 序列化为 JSON 字符串
    /// </summary>
    /// <returns>JSON 字符串表示</returns>
    public string ToJson()
    {
        var dto = new ExpressionContextDto
        {
            RawInput = RawInput,
            ParsedExpression = ParsedExpression,
            Parameters = Parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ClipboardContent = ClipboardContent,
            CreatedAt = CreatedAt
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>表达式上下文</returns>
    /// <exception cref="ArgumentNullException">当 json 为 null 时抛出</exception>
    /// <exception cref="JsonException">当 JSON 格式无效时抛出</exception>
    public static ExpressionContext FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var dto = JsonSerializer.Deserialize<ExpressionContextDto>(json, JsonOptions)
            ?? throw new JsonException("Failed to deserialize ExpressionContext: result was null.");

        if (string.IsNullOrWhiteSpace(dto.RawInput))
        {
            throw new JsonException("Failed to deserialize ExpressionContext: RawInput is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.ParsedExpression))
        {
            throw new JsonException("Failed to deserialize ExpressionContext: ParsedExpression is required.");
        }

        // 创建不可变的参数字典
        var immutableParams = dto.Parameters != null
            ? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(dto.Parameters))
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

        return new ExpressionContext(
            dto.RawInput,
            dto.ParsedExpression,
            immutableParams,
            dto.ClipboardContent,
            dto.CreatedAt);
    }

    /// <summary>
    /// 用于 JSON 序列化的 DTO 类
    /// </summary>
    private sealed class ExpressionContextDto
    {
        public string RawInput { get; set; } = string.Empty;
        public string ParsedExpression { get; set; } = string.Empty;
        public Dictionary<string, object?>? Parameters { get; set; }
        public string? ClipboardContent { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
