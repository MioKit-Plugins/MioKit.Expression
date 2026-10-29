namespace Expression.Interfaces;

/// <summary>
/// 脚本引擎工厂接口
/// 用于创建隔离的引擎实例
/// </summary>
public interface IScriptEngineFactory
{
    Serilog.ILogger Logger { get; }

    /// <summary>
    /// 获取搜索用脚本引擎（单例，全局共享）
    /// </summary>
    /// <returns>共享的搜索引擎实例</returns>
    IScriptEngine GetSearchEngine();

    /// <summary>
    /// 创建调试用脚本引擎（每次创建新实例，完全隔离）
    /// </summary>
    /// <returns>独立的调试引擎实例</returns>
    IScriptEngine CreateDebugEngine();
}
