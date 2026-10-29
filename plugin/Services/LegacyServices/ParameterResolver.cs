using System.Globalization;
using System.Text.RegularExpressions;
using Expression.Interfaces;
using Expression.Models;

namespace Expression.Services;

/// <summary>
/// 参数解析器实现
/// 负责解析和转换表达式参数
/// </summary>
public partial class ParameterResolver : IParameterResolver
{
    /// <summary>
    /// 剪贴板参数名称
    /// </summary>
    public const string ClipboardParamName = "clipboard";

    /// <summary>
    /// 剪贴板占位符名称
    /// </summary>
    public const string ClipboardPlaceholder = "[clipboard]";

    /// <summary>
    /// 参数分隔符（逗号、空格、制表符）
    /// </summary>
    private static readonly char[] ArgSeparators = [',', ' ', '\t'];

    /// <summary>
    /// 匹配带引号的字符串（单引号或双引号）
    /// </summary>
    [GeneratedRegex(@"^(['""])(.*)(\1)$", RegexOptions.Singleline)]
    private static partial Regex QuotedStringRegex();

    /// <inheritdoc />
    public IReadOnlyList<ParameterValue> Resolve(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        var trimmed = arguments.Trim();
        var results = new List<ParameterValue>();

        // 智能分割：处理引号内的逗号和空格
        var parts = SplitArguments(trimmed);

        foreach (var part in parts)
        {
            var paramValue = ParseSingleArgument(part);
            results.Add(paramValue);
        }

        return results;
    }

    /// <inheritdoc />
    public (string formatted, bool usePlaceholder) ResolveClipboard(string clipboard)
    {
        if (string.IsNullOrEmpty(clipboard))
        {
            return (string.Empty, false);
        }

        // 多行内容使用占位符
        if (IsMultiLine(clipboard))
        {
            return (ClipboardPlaceholder, true);
        }

        var trimmed = clipboard.Trim();

        // 单行：检查是否是完整的数学表达式（包含运算符），不应拆分
        if (IsMathExpression(trimmed))
        {
            return (trimmed, false);
        }

        // 解析剪贴板内容为参数
        var parameters = Resolve(clipboard);
        if (parameters.Count == 0)
        {
            return (trimmed, false);
        }

        // 格式化参数列表
        var formattedParts = parameters.Select(FormatParameterValue);
        return (string.Join(", ", formattedParts), false);
    }

    /// <inheritdoc />
    public string ResolvePlaceholder(string expression, string placeholderValue)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return expression;
        }

        if (string.IsNullOrEmpty(placeholderValue))
        {
            return expression.Replace(ClipboardPlaceholder, "''");
        }

        // 转义字符串中的单引号
        var escaped = placeholderValue.Replace("'", "\\'");
        var replacement = $"'{escaped}'";

        return expression.Replace(ClipboardPlaceholder, replacement);
    }

    /// <summary>
    /// 智能分割参数字符串
    /// 处理引号内的分隔符，保持引号内容完整
    /// </summary>
    private static List<string> SplitArguments(string input)
    {
        var results = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;
        var quoteChar = '\0';

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            // 处理引号
            if ((c == '"' || c == '\'') && (i == 0 || input[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = '\0';
                }
                current.Append(c);
                continue;
            }

            // 在引号内，保留所有字符
            if (inQuote)
            {
                current.Append(c);
                continue;
            }

            // 处理分隔符
            if (ArgSeparators.Contains(c))
            {
                if (current.Length > 0)
                {
                    results.Add(current.ToString().Trim());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        // 添加最后一个参数
        if (current.Length > 0)
        {
            results.Add(current.ToString().Trim());
        }

        return results;
    }

    /// <summary>
    /// 解析单个参数
    /// </summary>
    private static ParameterValue ParseSingleArgument(string arg)
    {
        var trimmed = arg.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return new ParameterValue
            {
                Value = null,
                Type = ParameterType.Unknown,
                RawText = arg
            };
        }

        // 检查是否是占位符
        if (trimmed.Equals(ClipboardPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return new ParameterValue
            {
                Value = trimmed,
                Type = ParameterType.Placeholder,
                RawText = arg
            };
        }

        // 检查是否是带引号的字符串
        var quotedMatch = QuotedStringRegex().Match(trimmed);
        if (quotedMatch.Success)
        {
            var content = quotedMatch.Groups[2].Value;
            // 处理转义的引号
            content = content.Replace("\\'", "'").Replace("\\\"", "\"");
            return new ParameterValue
            {
                Value = content,
                Type = ParameterType.String,
                RawText = arg
            };
        }

        // 尝试解析为数字
        if (TryParseNumber(trimmed, out var number))
        {
            return new ParameterValue
            {
                Value = number,
                Type = ParameterType.Number,
                RawText = arg
            };
        }

        // 默认作为字符串处理
        return new ParameterValue
        {
            Value = trimmed,
            Type = ParameterType.String,
            RawText = arg
        };
    }

    /// <summary>
    /// 尝试将字符串解析为数字
    /// 支持整数、小数、科学计数法
    /// </summary>
    private static bool TryParseNumber(string value, out decimal number)
    {
        // 尝试解析为 decimal
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        // 尝试解析为 double（处理科学计数法等特殊情况）
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
        {
            // 检查是否在 decimal 范围内
            if (doubleValue >= (double)decimal.MinValue && doubleValue <= (double)decimal.MaxValue)
            {
                number = (decimal)doubleValue;
                return true;
            }
        }

        number = 0;
        return false;
    }

    /// <summary>
    /// 检查是否为多行内容
    /// </summary>
    private static bool IsMultiLine(string value)
    {
        return value.Contains('\n') || value.Contains('\r');
    }

    /// <summary>
    /// 检查是否是数学表达式（而非参数列表）
    /// 数学表达式包含运算符，应作为整体传递，不应拆分
    /// </summary>
    private static bool IsMathExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // 检查括号是否匹配
        if (!AreBracketsBalanced(value))
            return false;

        // 检查是否包含数学运算符（排除引号内的）
        return ContainsMathOperator(value);
    }

    /// <summary>
    /// 检查是否包含数学运算符（排除引号内的）
    /// </summary>
    private static bool ContainsMathOperator(string value)
    {
        var inQuote = false;
        var quoteChar = '\0';
        var parenDepth = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            // 处理引号
            if ((c == '"' || c == '\'') && (i == 0 || value[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = '\0';
                }
                continue;
            }

            if (inQuote)
                continue;

            // 跟踪括号深度
            if (c == '(')
            {
                parenDepth++;
                continue;
            }
            if (c == ')')
            {
                parenDepth--;
                continue;
            }

            // 检查运算符（+、-、*、/、%、^）
            // 注意：+ 和 - 在数字开头时是正负号，不是运算符
            if (c == '*' || c == '/' || c == '%' || c == '^')
            {
                return true;
            }

            // + 和 - 需要特殊处理：前面必须是数字、右括号或标识符才是运算符
            if (c == '+' || c == '-')
            {
                if (i > 0)
                {
                    var prev = value[i - 1];
                    // 前一个字符是数字、右括号、字母或下划线，说明是二元运算符
                    if (char.IsDigit(prev) || prev == ')' || char.IsLetter(prev) || prev == '_')
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 检查括号是否匹配
    /// </summary>
    private static bool AreBracketsBalanced(string value)
    {
        var count = 0;
        var inQuote = false;
        var quoteChar = '\0';

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            // 处理引号
            if ((c == '"' || c == '\'') && (i == 0 || value[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuote = false;
                    quoteChar = '\0';
                }
                continue;
            }

            if (inQuote)
                continue;

            if (c == '(') count++;
            else if (c == ')') count--;

            if (count < 0) return false;
        }

        return count == 0;
    }

    /// <summary>
    /// 格式化参数值为表达式字符串
    /// </summary>
    private static string FormatParameterValue(ParameterValue param)
    {
        return param.Type switch
        {
            ParameterType.Number => param.Value?.ToString() ?? "0",
            ParameterType.String => FormatStringValue(param.Value?.ToString() ?? string.Empty),
            ParameterType.Placeholder => param.Value?.ToString() ?? ClipboardPlaceholder,
            _ => param.RawText
        };
    }

    /// <summary>
    /// 格式化字符串值（添加引号并转义）
    /// </summary>
    private static string FormatStringValue(string value)
    {
        var escaped = value.Replace("'", "\\'");
        return $"'{escaped}'";
    }
}
