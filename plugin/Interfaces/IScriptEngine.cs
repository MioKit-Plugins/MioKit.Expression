namespace Expression.Interfaces;

/// <summary>
/// 统一的脚本引擎接口
/// 用于搜索和调试场景的脚本执行
/// </summary>
public interface IScriptEngine : IDisposable
{
    /// <summary>
    /// 引擎唯一标识（用于区分不同实例）
    /// </summary>
    string EngineId { get; }

    /// <summary>
    /// 引擎是否运行中
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 可用的自定义函数
    /// </summary>
    IReadOnlySet<string> AvailableFunctions { get; }

    /// <summary>
    /// 启动引擎
    /// </summary>
    /// <param name="userScript">用户自定义的JS代码，可选</param>
    /// <returns>启动是否成功</returns>
    Task<bool> StartAsync(string? userScript = null);

    /// <summary>
    /// 停止引擎
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 重启引擎
    /// </summary>
    /// <param name="userScript">用户自定义的JS代码，可选</param>
    /// <returns>重启是否成功</returns>
    Task<bool> RestartAsync(string? userScript = null);

    /// <summary>
    /// 检查函数是否可调用
    /// </summary>
    /// <param name="functionName">函数名称</param>
    /// <returns>如果函数存在且引擎正在运行则返回true</returns>
    bool CanInvoke(string functionName);

    /// <summary>
    /// 调用函数
    /// </summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="args">函数参数</param>
    /// <returns>函数执行结果</returns>
    object? Invoke(string functionName, params object?[] args);

    /// <summary>
    /// 异步调用函数
    /// </summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="args">函数参数</param>
    /// <returns>函数执行结果</returns>
    Task<object?> InvokeAsync(string functionName, params object?[] args);

    /// <summary>
    /// 脚本引擎状态变化事件
    /// </summary>
    event EventHandler<ScriptStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 脚本执行错误事件
    /// </summary>
    event EventHandler<ScriptErrorEventArgs>? Error;

    /// <summary>
    /// JS console 输出事件
    /// </summary>
    event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput;
}


/// <summary>
/// 脚本引擎状态
/// </summary>
public enum ScriptState
{
    /// <summary>
    /// 已停止
    /// </summary>
    Stopped,

    /// <summary>
    /// 正在启动
    /// </summary>
    Starting,

    /// <summary>
    /// 运行中
    /// </summary>
    Running,

    /// <summary>
    /// 正在停止
    /// </summary>
    Stopping
}

/// <summary>
/// 脚本引擎状态变化事件参数
/// </summary>
public class ScriptStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 旧状态
    /// </summary>
    public ScriptState OldState { get; }

    /// <summary>
    /// 新状态
    /// </summary>
    public ScriptState NewState { get; }

    public ScriptStateChangedEventArgs(ScriptState oldState, ScriptState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// 脚本执行错误事件参数
/// </summary>
public class ScriptErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误信息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 异常对象
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// 发生错误的函数名称
    /// </summary>
    public string? FunctionName { get; }

    public ScriptErrorEventArgs(string message, Exception? exception = null, string? functionName = null)
    {
        Message = message;
        Exception = exception;
        FunctionName = functionName;
    }
}

/// <summary>
/// Console输出级别
/// </summary>
public enum ConsoleOutputLevel
{
    Log,
    Info,
    Warn,
    Error,
    Debug
}

/// <summary>
/// JS console 输出事件参数
/// </summary>
public class ConsoleOutputEventArgs : EventArgs
{
    /// <summary>
    /// 输出级别
    /// </summary>
    public ConsoleOutputLevel Level { get; }

    /// <summary>
    /// 输出消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 原始参数
    /// </summary>
    public object?[] Arguments { get; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; }

    public ConsoleOutputEventArgs(ConsoleOutputLevel level, string message, object?[] arguments)
    {
        Level = level;
        Message = message;
        Arguments = arguments;
        Timestamp = DateTime.Now;
    }
}
