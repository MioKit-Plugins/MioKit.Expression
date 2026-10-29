using Microsoft.ClearScript.V8;
using Expression.Interfaces;
using Serilog;

namespace Expression.Services;

/// <summary>
/// 脚本引擎工厂实现
/// 管理搜索引擎单例和创建独立的调试引擎
/// </summary>
public class ScriptEngineFactory : IScriptEngineFactory, IDisposable
{
    private readonly ILogger _logger;
    private readonly V8Runtime _runtime;
    private readonly object _lock = new();
    
    private ScriptEngine? _searchEngine;
    private bool _disposed;

    /// <summary>
    /// 搜索引擎的固定ID
    /// </summary>
    private const string SearchEngineId = "search";

    /// <summary>
    /// 创建脚本引擎工厂
    /// </summary>
    /// <param name="runtime">V8运行时（通过DI注入）</param>
    public ScriptEngineFactory(V8Runtime runtime, ExpressionPlugin plugin)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = plugin.Context.Logger.ForContext<ScriptEngineFactory>();
    }

    public ILogger Logger => _logger;

    /// <inheritdoc />
    public IScriptEngine GetSearchEngine()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScriptEngineFactory));

        lock (_lock)
        {
            if (_searchEngine == null)
            {
                _logger.Information("Creating search script engine (singleton)");
                _searchEngine = new ScriptEngine(_runtime, SearchEngineId, _logger);
            }

            return _searchEngine;
        }
    }

    /// <inheritdoc />
    public IScriptEngine CreateDebugEngine()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScriptEngineFactory));

        var debugEngineId = $"debug-{Guid.NewGuid():N}";
        _logger.Information("Creating debug script engine with ID: {EngineId}", debugEngineId);
        
        return new ScriptEngine(_runtime, debugEngineId, _logger);
    }

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
                _searchEngine?.Dispose();
                _searchEngine = null;
            }
            
            _logger.Information("ScriptEngineFactory disposed");
        }

        _disposed = true;
    }

    ~ScriptEngineFactory()
    {
        Dispose(false);
    }

    #endregion
}
