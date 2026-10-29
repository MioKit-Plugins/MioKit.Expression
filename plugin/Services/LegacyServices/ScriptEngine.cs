using System.Text;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Expression.Interfaces;
using Serilog;
using System.Text.Json;

namespace Expression.Services;

/// <summary>
/// 脚本引擎实现
/// 基于 V8 引擎，用户需要在 window.expression 对象中定义方法
/// </summary>
public class ScriptEngine : Interfaces.IScriptEngine
{
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private readonly V8Runtime _runtime;
    private readonly string _engineId;

    private V8ScriptEngine? _engine;
    private ScriptState _state = ScriptState.Stopped;
    private HashSet<string> _availableFunctions = [];
    private bool _disposed;

    /// <summary>
    /// V8引擎配置标志
    /// </summary>
    private readonly V8ScriptEngineFlags _flags =
        V8ScriptEngineFlags.EnableTaskPromiseConversion |
        V8ScriptEngineFlags.EnableArrayConversion |
        V8ScriptEngineFlags.EnableValueTaskPromiseConversion;

    /// <summary>
    /// 用户自定义方法的容器名称
    /// </summary>
    private const string UserExpressionContainer = "expression";

    /// <summary>
    /// 创建脚本引擎实例
    /// </summary>
    /// <param name="runtime">V8运行时</param>
    /// <param name="engineId">引擎唯一标识</param>
    public ScriptEngine(V8Runtime runtime, string engineId, ILogger logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _engineId = engineId ?? throw new ArgumentNullException(nameof(engineId));
        _logger = logger.ForContext<ScriptEngine>();
    }

    #region IScriptEngine Properties

    /// <inheritdoc />
    public string EngineId => _engineId;

    /// <inheritdoc />
    public bool IsRunning => _state == ScriptState.Running;

    /// <inheritdoc />
    public IReadOnlySet<string> AvailableFunctions => _availableFunctions;

    /// <summary>
    /// 当前引擎状态
    /// </summary>
    public ScriptState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            var oldState = _state;
            _state = value;
            OnStateChanged(oldState, value);
        }
    }

    #endregion

    #region IScriptEngine Events

    /// <inheritdoc />
    public event EventHandler<ScriptStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ScriptErrorEventArgs>? Error;

    /// <inheritdoc />
    public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput;

    #endregion


    #region IScriptEngine Methods

    /// <inheritdoc />
    public bool CanInvoke(string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName)) return false;
        return IsRunning && _engine != null && _availableFunctions.Contains(functionName);
    }

    /// <inheritdoc />
    public object? Invoke(string functionName, params object?[] args)
    {
        if (!CanInvoke(functionName))
        {
            _logger.Warning("[{EngineId}] Cannot invoke function '{FunctionName}': engine not ready or function not found",
                _engineId, functionName);
            return null;
        }

        try
        {
            lock (_lock)
            {
                if (_engine == null) return null;

                // 预处理参数：将 decimal 转为 double 避免精度问题
                var processedArgs = args.Select(ConvertArgument).ToArray();
                var serializedArgs = JsonSerializer.Serialize(processedArgs);
                var result = _engine.Invoke("__invoke", functionName, serializedArgs);

                // 处理返回值
                return ConvertResult(result);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{EngineId}] Failed to invoke function '{FunctionName}'", _engineId, functionName);
            OnError($"调用函数 '{functionName}' 失败: {e.Message}", e, functionName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<object?> InvokeAsync(string functionName, params object?[] args)
    {
        return await Task.Run(() => Invoke(functionName, args));
    }

    /// <inheritdoc />
    public async Task<bool> StartAsync(string? userScript = null)
    {
        if (IsRunning)
        {
            _logger.Information("[{EngineId}] Script engine is already running", _engineId);
            return true;
        }

        try
        {
            State = ScriptState.Starting;
            _logger.Information("[{EngineId}] Starting script engine...", _engineId);

            // 加载系统内置JS代码
            var systemCode = await LoadSystemScriptAsync();
            if (string.IsNullOrEmpty(systemCode))
            {
                _logger.Error("[{EngineId}] Failed to load system script", _engineId);
                State = ScriptState.Stopped;
                return false;
            }

            lock (_lock)
            {
                // 清理旧引擎
                DisposeEngine();

                // 创建新引擎（使用唯一名称确保隔离）
                _engine = _runtime.CreateScriptEngine(_engineId, _flags);
                _engine.Execute("globalThis.window = globalThis;");

                // Hook console 方法
                SetupConsoleHook();

                // 执行系统脚本
                _engine.Execute(systemCode);

                // 执行用户代码
                if (!string.IsNullOrWhiteSpace(userScript))
                {
                    // Let startup fail and clean up the engine instead of
                    // reporting a successful reload with a partially loaded script.
                    _engine.Execute(userScript);
                }

                // 提取用户自定义方法
                _availableFunctions = ExtractAvailableFunctions();
            }

            State = ScriptState.Running;
            _logger.Information(
                "[{EngineId}] Script engine started successfully, found {Count} functions: {Functions}",
                _engineId, _availableFunctions.Count, string.Join(", ", _availableFunctions));

            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{EngineId}] Failed to start script engine", _engineId);
            OnError("启动脚本引擎失败", e);
            await StopAsync();
            return false;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_state == ScriptState.Stopped)
        {
            return;
        }

        try
        {
            State = ScriptState.Stopping;
            _logger.Information("[{EngineId}] Stopping script engine...", _engineId);

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    DisposeEngine();
                    _availableFunctions = [];
                }
            });

            State = ScriptState.Stopped;
            _logger.Information("[{EngineId}] Script engine stopped", _engineId);
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{EngineId}] Error occurred while stopping script engine", _engineId);
            State = ScriptState.Stopped;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RestartAsync(string? userScript = null)
    {
        _logger.Information("[{EngineId}] Restarting script engine...", _engineId);
        await StopAsync();
        return await StartAsync(userScript);
    }

    #endregion


    #region Private Methods

    /// <summary>
    /// 转换参数类型
    /// </summary>
    private static object? ConvertArgument(object? arg)
    {
        return arg switch
        {
            decimal d => (double)d, // JS 不支持 decimal，转为 double
            _ => arg
        };
    }

    /// <summary>
    /// 转换JS返回值
    /// </summary>
    private static object? ConvertResult(object? result)
    {
        return result switch
        {
            // V8 返回的整数可能是 int 或 long
            int i => (decimal)i,
            long l => (decimal)l,
            // V8 返回的浮点数是 double
            double d => (decimal)d,
            // 字符串和其他类型直接返回
            _ => result
        };
    }

    /// <summary>
    /// 加载系统内置JS脚本
    /// </summary>
    private async Task<string?> LoadSystemScriptAsync()
    {
        try
        {
            await using var stream = GetType().Assembly
                .GetManifestResourceStream("Expression.Assets.systemFunc.js");

            if (stream == null)
            {
                _logger.Error("[{EngineId}] System script resource not found", _engineId);
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{EngineId}] Failed to load system script", _engineId);
            return null;
        }
    }

    /// <summary>
    /// 从 window.expression 对象中提取可用函数
    /// </summary>
    private HashSet<string> ExtractAvailableFunctions()
    {
        var functions = new HashSet<string>();

        if (_engine == null) return functions;

        try
        {
            // 获取 window.expression 对象
            var expressionObj = _engine.Global.GetProperty(UserExpressionContainer);
            if (expressionObj == null || expressionObj is Undefined)
            {
                _logger.Information("[{EngineId}] No window.expression object found, user functions will not be available", _engineId);
                return functions;
            }

            // 转换为 ScriptObject
            if (expressionObj is not ScriptObject scriptObject)
            {
                _logger.Warning("[{EngineId}] window.expression is not a valid object", _engineId);
                return functions;
            }

            // 遍历属性，找出所有函数
            foreach (var propName in scriptObject.PropertyNames)
            {
                var name = propName?.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                try
                {
                    var propValue = scriptObject.GetProperty(name);

                    // 检查是否为函数
                    if (propValue is IJavaScriptObject jsObject && jsObject.Kind == JavaScriptObjectKind.Function)
                    {
                        functions.Add(name);
                        _logger.Debug("[{EngineId}] Found function: {FunctionName}", _engineId, name);
                    }
                }
                catch (Exception e)
                {
                    _logger.Warning(e, "[{EngineId}] Failed to check property '{PropertyName}'", _engineId, name);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "[{EngineId}] Failed to extract functions from window.expression", _engineId);
            OnError("提取用户函数失败", e);
        }

        return functions;
    }

    /// <summary>
    /// 释放引擎资源
    /// </summary>
    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    /// <summary>
    /// 触发状态变化事件
    /// </summary>
    private void OnStateChanged(ScriptState oldState, ScriptState newState)
    {
        StateChanged?.Invoke(this, new ScriptStateChangedEventArgs(oldState, newState));
    }

    /// <summary>
    /// 触发错误事件
    /// </summary>
    private void OnError(string message, Exception? exception = null, string? functionName = null)
    {
        Error?.Invoke(this, new ScriptErrorEventArgs(message, exception, functionName));
    }

    /// <summary>
    /// 触发Console输出事件
    /// </summary>
    private void OnConsoleOutput(ConsoleOutputLevel level, object?[] args)
    {
        var message = string.Join(" ", args.Select(a => a?.ToString() ?? "null"));
        ConsoleOutput?.Invoke(this, new ConsoleOutputEventArgs(level, message, args));
    }

    /// <summary>
    /// 设置console方法的hook
    /// </summary>
    private void SetupConsoleHook()
    {
        if (_engine == null) return;

        // 创建console代理对象
        var consoleProxy = new ConsoleProxy(this);
        _engine.AddHostObject("console", consoleProxy);
    }

    #endregion


    #region Console Proxy

    /// <summary>
    /// Console代理类，用于拦截JS的console调用
    /// 使用 WeakReference 避免循环引用导致的内存泄漏
    /// </summary>
    public class ConsoleProxy
    {
        private readonly WeakReference<ScriptEngine> _engineRef;

        public ConsoleProxy(ScriptEngine engine)
        {
            _engineRef = new WeakReference<ScriptEngine>(engine);
        }

        public void log(params object?[] args)
        {
            if (_engineRef.TryGetTarget(out var engine))
                engine.OnConsoleOutput(ConsoleOutputLevel.Log, args);
        }

        public void info(params object?[] args)
        {
            if (_engineRef.TryGetTarget(out var engine))
                engine.OnConsoleOutput(ConsoleOutputLevel.Info, args);
        }

        public void warn(params object?[] args)
        {
            if (_engineRef.TryGetTarget(out var engine))
                engine.OnConsoleOutput(ConsoleOutputLevel.Warn, args);
        }

        public void error(params object?[] args)
        {
            if (_engineRef.TryGetTarget(out var engine))
                engine.OnConsoleOutput(ConsoleOutputLevel.Error, args);
        }

        public void debug(params object?[] args)
        {
            if (_engineRef.TryGetTarget(out var engine))
                engine.OnConsoleOutput(ConsoleOutputLevel.Debug, args);
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            lock (_lock)
            {
                DisposeEngine();
                _availableFunctions = [];
            }

            State = ScriptState.Stopped;
        }

        _disposed = true;
    }

    ~ScriptEngine()
    {
        Dispose(false);
    }

    #endregion
}
