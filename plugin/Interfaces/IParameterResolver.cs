using Expression.Models;

namespace Expression.Interfaces;

/// <summary>
/// 参数解析器接口
/// 负责解析和转换表达式参数
/// </summary>
public interface IParameterResolver
{
    /// <summary>
    /// 解析参数字符串
    /// 支持逗号分隔和空格分隔的参数
    /// </summary>
    /// <param name="arguments">参数字符串，如 "1, 2, 3" 或 "1 2 3"</param>
    /// <returns>解析后的参数值列表</returns>
    IReadOnlyList<ParameterValue> Resolve(string arguments);

    /// <summary>
    /// 解析剪贴板内容为参数
    /// </summary>
    /// <param name="clipboard">剪贴板内容</param>
    /// <returns>
    /// formatted: 格式化后的参数字符串
    /// usePlaceholder: 是否需要使用占位符（当内容包含换行等复杂字符时）
    /// </returns>
    (string formatted, bool usePlaceholder) ResolveClipboard(string clipboard);

    /// <summary>
    /// 替换表达式中的占位符
    /// </summary>
    /// <param name="expression">包含占位符的表达式</param>
    /// <param name="placeholderValue">占位符的实际值</param>
    /// <returns>替换后的表达式</returns>
    string ResolvePlaceholder(string expression, string placeholderValue);
}
