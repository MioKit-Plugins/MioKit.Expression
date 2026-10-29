namespace Expression;

/// <summary>
/// 可从节点选择器独立执行的表达式入口；不包含依赖搜索结果载荷的计算结果节点。
/// </summary>
[NodePalette("表达式计算")]
public interface IExpressionEntryFeature : IInvokeFeature;
