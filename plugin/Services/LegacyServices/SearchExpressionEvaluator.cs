using Expression.Interfaces;

namespace Expression.Services;

/// <summary>
/// 搜索框表达式计算服务
/// 使用全局共享的搜索引擎，不负责其生命周期管理
/// </summary>
public class SearchExpressionEvaluator : ExpressionEvaluator
{
    private readonly IScriptEngine _searchEngine;

    /// <summary>
    /// 通过工厂获取共享的搜索引擎
    /// </summary>
    /// <param name="factory">脚本引擎工厂</param>
    public SearchExpressionEvaluator(IScriptEngineFactory factory)
        : base(factory.GetSearchEngine(), ownsScriptEngine: false, factory.Logger)
    {
        _searchEngine = factory.GetSearchEngine();
    }

    /// <summary>
    /// 搜索引擎实例
    /// </summary>
    public IScriptEngine SearchEngine => _searchEngine;

    /// <summary>
    /// 可用的自定义函数
    /// </summary>
    public IReadOnlySet<string> AvailableFunctions => _searchEngine.AvailableFunctions;
}
