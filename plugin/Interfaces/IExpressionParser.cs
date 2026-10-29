using Expression.Models;

namespace Expression.Interfaces;

/// <summary>
/// 表达式解析器接口
/// 负责将原始用户输入解析为结构化的表达式
/// </summary>
public interface IExpressionParser
{
    /// <summary>
    /// 解析用户输入和剪贴板内容
    /// </summary>
    /// <param name="userInput">用户输入的文本</param>
    /// <param name="clipboardText">剪贴板内容（可选）</param>
    /// <returns>解析结果，包含表达式、函数名、参数等信息</returns>
    ParseResult Parse(string? userInput, string? clipboardText);

    /// <summary>
    /// 检查输入是否可能是表达式
    /// 仅用于快速过滤明显不是表达式的内容
    /// </summary>
    /// <param name="input">待检查的输入</param>
    /// <returns>如果输入看起来像表达式则返回 true</returns>
    bool IsLikelyExpression(string? input);
}
