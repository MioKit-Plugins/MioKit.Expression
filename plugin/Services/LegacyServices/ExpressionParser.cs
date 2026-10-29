using System.Text.RegularExpressions;
using Expression.Interfaces;
using Expression.Models;

namespace Expression.Services;

/// <summary>
/// 表达式解析器实现
/// 负责将原始用户输入解析为结构化的表达式
/// </summary>
public partial class ExpressionParser : IExpressionParser
{
    private readonly IParameterResolver _parameterResolver;

    #region Generated Regex

    /// <summary>
    /// 函数调用正则：匹配 funcName(args) 格式
    /// </summary>
    [GeneratedRegex(@"^([A-Za-z_]\w*)\s*\((.*)\)$", RegexOptions.Singleline)]
    private static partial Regex FunctionCallRegex();

    /// <summary>
    /// 标识符正则：匹配有效的函数名
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z_]\w*$")]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// 纯数字（含小数/科学计数法），用于过滤「只是一个数字」的情况
    /// </summary>
    [GeneratedRegex(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$")]
    private static partial Regex PureNumberRegex();

    /// <summary>
    /// NCalc 支持的特殊字面量：DateTime/TimeSpan 形式
    /// </summary>
    [GeneratedRegex(@"^#.+#$")]
    private static partial Regex HashDateOrTimeRegex();

    /// <summary>
    /// Guid 字面量
    /// </summary>
    [GeneratedRegex(@"^[{(]?[0-9A-Fa-f]{8}(-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}[)}]?$")]
    private static partial Regex GuidRegex();

    /// <summary>
    /// 数学 / 比较 / 逻辑运算符
    /// </summary>
    [GeneratedRegex(@"[+\-*/%^<>!=&|?:]")]
    private static partial Regex OperatorRegex();

    /// <summary>
    /// 逻辑关键字
    /// </summary>
    [GeneratedRegex(@"\b(and|or|not|in|like|true|false)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LogicalKeywordRegex();

    /// <summary>
    /// 函数调用特征：以标识符开头，后跟左括号
    /// </summary>
    [GeneratedRegex(@"[A-Za-z_]\w*\s*\(")]
    private static partial Regex FunctionCallFeatureRegex();

    /// <summary>
    /// 十六进制 / 二进制 / 八进制数字前缀
    /// </summary>
    [GeneratedRegex(@"^(0x[0-9A-Fa-f]+|0b[01]+|0o[0-7]+)$")]
    private static partial Regex PrefixedNumberRegex();

    /// <summary>
    /// 纯列表模式：(数字, 数字, ...)
    /// </summary>
    [GeneratedRegex(@"^\(\s*[+-]?[\d.]+(?:\s*,\s*[+-]?[\d.]+)+\s*\)$")]
    private static partial Regex PureListRegex();

    #endregion

    public ExpressionParser(IParameterResolver parameterResolver)
    {
        _parameterResolver = parameterResolver;
    }

    /// <inheritdoc />
    public ParseResult Parse(string? userInput, string? clipboardText)
    {
        var trimmedInput = userInput?.Trim();
        var trimmedClipboard = clipboardText?.Trim();

        // 1. 检查空输入
        if (string.IsNullOrWhiteSpace(trimmedInput) && string.IsNullOrWhiteSpace(trimmedClipboard))
        {
            return ParseResult.Fail(ErrorCodes.EmptyInput, "输入为空");
        }

        // 2. 只有剪贴板内容
        if (string.IsNullOrWhiteSpace(trimmedInput) && !string.IsNullOrWhiteSpace(trimmedClipboard))
        {
            // 多行内容不进行运算
            if (IsMultiLine(clipboardText!))
            {
                return ParseResult.Fail(ErrorCodes.InvalidSyntax, "多行内容不支持直接运算");
            }
            return ParseExpression(trimmedClipboard!);
        }

        // 3. 只有用户输入
        if (!string.IsNullOrWhiteSpace(trimmedInput) && string.IsNullOrWhiteSpace(trimmedClipboard))
        {
            return ParseExpression(trimmedInput!);
        }

        // 4. 都有：尝试组合用户输入和剪贴板
        return ParseWithClipboard(trimmedInput!, trimmedClipboard!);
    }

    /// <summary>
    /// 检查是否为多行内容
    /// </summary>
    private static bool IsMultiLine(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }

    /// <inheritdoc />
    public bool IsLikelyExpression(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();

        // 1. 单纯数字（含前缀进制）认为只是数值输入，不当作表达式
        if (PureNumberRegex().IsMatch(text) || PrefixedNumberRegex().IsMatch(text))
        {
            return false;
        }

        // 2. 纯列表模式 (1,2,3) 不是有效的数学表达式
        if (PureListRegex().IsMatch(text))
        {
            return false;
        }

        // 3. 明确的特殊字面量：DateTime / TimeSpan / Guid，允许作为表达式
        if (HashDateOrTimeRegex().IsMatch(text) || GuidRegex().IsMatch(text))
        {
            return true;
        }

        // 4. 有函数调用特征
        if (FunctionCallFeatureRegex().IsMatch(text))
        {
            return true;
        }

        // 5. 包含逻辑关键词
        if (LogicalKeywordRegex().IsMatch(text))
        {
            return true;
        }

        // 6. 包含运算符
        if (OperatorRegex().IsMatch(text))
        {
            return true;
        }

        // 7. 包含括号但不是纯列表，可能是分组表达式如 (1+2)
        if (text.Contains('(') && text.Contains(')'))
        {
            return true;
        }

        // 其它形态（例如单个标识符 foo）仍然交给后续逻辑视为"不是表达式"
        return false;
    }

    /// <summary>
    /// 解析单个表达式字符串
    /// </summary>
    private ParseResult ParseExpression(string expression)
    {
        // 尝试解析为函数调用
        var funcMatch = FunctionCallRegex().Match(expression);
        if (funcMatch.Success)
        {
            var funcName = funcMatch.Groups[1].Value;
            var argsString = funcMatch.Groups[2].Value;
            var args = ParseArgumentList(argsString);

            return ParseResult.Ok(expression, funcName, args);
        }

        // 不是函数调用，作为普通表达式返回
        return ParseResult.Ok(expression);
    }

    /// <summary>
    /// 解析用户输入 + 剪贴板组合
    /// </summary>
    private ParseResult ParseWithClipboard(string userInput, string clipboard)
    {
        // 如果用户输入已经是完整表达式（包含括号），直接解析
        if (userInput.Contains('('))
        {
            return ParseExpression(userInput);
        }

        // 检查用户输入是否是有效的函数名（标识符）
        if (!IdentifierRegex().IsMatch(userInput))
        {
            // 不是函数名，作为普通表达式处理
            return ParseExpression(userInput);
        }

        // 用户输入是函数名，剪贴板是参数
        // 例如：用户输入 "add"，剪贴板 "1,2,3" => "add(1,2,3)"
        var (formattedArgs, usePlaceholder) = _parameterResolver.ResolveClipboard(clipboard);

        string expression;
        if (usePlaceholder)
        {
            // 复杂内容使用占位符
            expression = $"{userInput}({formattedArgs})";
        }
        else
        {
            expression = $"{userInput}({formattedArgs})";
        }

        // 解析参数列表
        var args = ParseArgumentList(formattedArgs);

        return ParseResult.Ok(expression, userInput, args);
    }

    /// <summary>
    /// 解析参数列表字符串为字符串数组
    /// </summary>
    private IReadOnlyList<string> ParseArgumentList(string argsString)
    {
        if (string.IsNullOrWhiteSpace(argsString))
        {
            return [];
        }

        var parameters = _parameterResolver.Resolve(argsString);
        return parameters.Select(p => p.RawText).ToList();
    }
}
