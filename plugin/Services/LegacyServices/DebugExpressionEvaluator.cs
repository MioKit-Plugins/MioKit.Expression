using Expression.Interfaces;

namespace Expression.Services;

/// <summary>
/// 调试用表达式计算服务
/// 拥有独立的调试引擎实例，负责完整生命周期管理
/// </summary>
public class DebugExpressionEvaluator : ExpressionEvaluator
{
    private readonly IScriptEngine _debugEngine;

    /// <summary>
    /// 通过工厂创建独立的调试引擎
    /// </summary>
    /// <param name="factory">脚本引擎工厂</param>
    public DebugExpressionEvaluator(IScriptEngineFactory factory)
        : this(factory.CreateDebugEngine(), factory.Logger)
    {
    }

    /// <summary>
    /// 使用指定的脚本引擎创建调试求值器
    /// </summary>
    /// <param name="debugEngine">调试引擎实例</param>
    private DebugExpressionEvaluator(IScriptEngine debugEngine, Serilog.ILogger logger)
        : base(debugEngine, ownsScriptEngine: true, logger)
    {
        _debugEngine = debugEngine;
    }

    /// <summary>
    /// 调试引擎实例
    /// </summary>
    public IScriptEngine DebugEngine => _debugEngine;

    /// <summary>
    /// 脚本是否正在运行
    /// </summary>
    public bool IsScriptRunning => _debugEngine.IsRunning;

    /// <summary>
    /// 可用的自定义函数
    /// </summary>
    public IReadOnlySet<string> AvailableFunctions => _debugEngine.AvailableFunctions;

    /// <summary>
    /// 启动脚本引擎
    /// </summary>
    /// <param name="jsCode">JS代码</param>
    /// <returns>是否启动成功</returns>
    public Task<bool> StartAsync(string? jsCode = null)
    {
        return _debugEngine.StartAsync(jsCode);
    }

    /// <summary>
    /// 停止脚本引擎
    /// </summary>
    public Task StopAsync()
    {
        return _debugEngine.StopAsync();
    }

    /// <summary>
    /// 重启脚本引擎（更新代码时使用）
    /// </summary>
    /// <param name="jsCode">新的JS代码</param>
    /// <returns>是否重启成功</returns>
    public Task<bool> RestartAsync(string? jsCode = null)
    {
        return _debugEngine.RestartAsync(jsCode);
    }

    #region Events

    /// <summary>
    /// 订阅Console输出事件
    /// </summary>
    public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput
    {
        add => _debugEngine.ConsoleOutput += value;
        remove => _debugEngine.ConsoleOutput -= value;
    }

    /// <summary>
    /// 订阅脚本错误事件
    /// </summary>
    public event EventHandler<ScriptErrorEventArgs>? Error
    {
        add => _debugEngine.Error += value;
        remove => _debugEngine.Error -= value;
    }

    /// <summary>
    /// 订阅脚本状态变化事件
    /// </summary>
    public event EventHandler<ScriptStateChangedEventArgs>? StateChanged
    {
        add => _debugEngine.StateChanged += value;
        remove => _debugEngine.StateChanged -= value;
    }

    #endregion
}
