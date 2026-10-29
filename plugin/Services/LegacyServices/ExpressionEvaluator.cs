using System.Diagnostics;
using System.Text.RegularExpressions;
using Expression.Interfaces;
using Expression.Models;
using NCalc;
using NCalc.Handlers;
using Serilog;

namespace Expression.Services;

/// <summary>
/// 表达式求值器
/// 使用组合模式，通过构造函数注入脚本引擎
/// </summary>
public class ExpressionEvaluator : IDisposable
{
    private readonly ILogger _logger;
    private readonly IScriptEngine? _scriptEngine;
    private readonly bool _ownsScriptEngine;

    private bool _disposed;


    /// <summary>
    /// 纯数值正则：整数、小数、科学计数法
    /// </summary>
    private static readonly Regex PureNumberRegex = new(
        @"^\s*[+-]?\d+(\.\d+)?([eE][+-]?\d+)?\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// 函数名提取正则
    /// </summary>
    private static readonly Regex FunctionRegex = new(
        @"([a-zA-Z_][a-zA-Z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// 百分比正则：匹配 10% 或 10.5% 形式，但 % 后面不能紧跟数字（避免与取模混淆）
    /// </summary>
    private static readonly Regex PercentRegex = new(
        @"(\d+(?:\.\d+)?)\s*%(?!\s*\d)",
        RegexOptions.Compiled);

    /// <summary>
    /// NCalc内置函数
    /// </summary>
    private static readonly HashSet<string> BuiltInFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "acos", "asin", "atan", "ceiling", "cos", "exp", "floor",
        "ieeeremainder", "log", "log10", "max", "min", "pow", "round",
        "sign", "sin", "sqrt", "tan", "truncate", "if", "in"
    };

    /// <summary>
    /// 获取注入的脚本引擎
    /// </summary>
    protected IScriptEngine? ScriptEngine => _scriptEngine;

    #region Constructors

    /// <summary>
    /// 创建求值器（可选脚本引擎）
    /// </summary>
    /// <param name="scriptEngine">脚本引擎实例</param>
    /// <param name="ownsScriptEngine">是否拥有引擎生命周期（true 时 Dispose 会释放引擎）</param>
    public ExpressionEvaluator(
        IScriptEngine? scriptEngine,
        bool ownsScriptEngine,
        ILogger logger)
    {
        _scriptEngine = scriptEngine;
        _ownsScriptEngine = ownsScriptEngine;
        _logger = logger.ForContext<ExpressionEvaluator>();
    }

    #endregion

    #region Expression Validation

    /// <summary>
    /// 检查表达式是否可以执行
    /// </summary>
    public bool CanExecute(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var trimmed = expression.Trim();

        // 预处理：转换百分比
        var processed = PreprocessExpression(trimmed);

        // 纯数值不可执行
        if (IsPureNumber(processed))
            return false;

        // 语法检查
        if (!IsValidSyntax(processed, out var functions))
            return false;

        // 检查自定义函数是否都存在
        if (functions.Count > 0 && !AllFunctionsAvailable(functions))
            return false;

        return true;
    }

    /// <summary>
    /// 检查是否为纯数值
    /// </summary>
    private bool IsPureNumber(string expression) => PureNumberRegex.IsMatch(expression);

    /// <summary>
    /// 检查表达式语法是否有效
    /// </summary>
    private bool IsValidSyntax(string expression, out HashSet<string> functions)
    {
        functions = [];
        try
        {
            var expr = new NCalc.Expression(expression);
            if (expr.HasErrors())
                return false;

            ExtractFunctions(expression, functions);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    /// <summary>
    /// 提取表达式中的自定义函数名
    /// </summary>
    private void ExtractFunctions(string expression, HashSet<string> functions)
    {
        var matches = FunctionRegex.Matches(expression);
        foreach (Match match in matches)
        {
            var funcName = match.Groups[1].Value;
            if (!BuiltInFunctions.Contains(funcName))
            {
                functions.Add(funcName);
            }
        }
    }

    /// <summary>
    /// 检查所有自定义函数是否可用
    /// </summary>
    private bool AllFunctionsAvailable(HashSet<string> functions)
    {
        var engine = _scriptEngine;
        if (engine == null || !engine.IsRunning)
            return false;

        return functions.All(func => engine.CanInvoke(func));
    }

    #endregion


    #region Expression Evaluation

    /// <summary>
    /// 异步求值表达式（使用 ExpressionContext，返回新版结果类型）
    /// </summary>
    /// <param name="context">表达式上下文</param>
    /// <returns>求值结果</returns>
    public async Task<EvaluationResult> EvaluateContextAsync(Models.ExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var parameters = context.Parameters.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);

        return await EvaluateToResultAsync(context.ParsedExpression, parameters);
    }

    /// <summary>
    /// 异步求值表达式（返回新版结果类型）
    /// </summary>
    /// <param name="expression">表达式字符串</param>
    /// <param name="parameters">参数字典（可选）</param>
    /// <returns>求值结果</returns>
    public async Task<EvaluationResult> EvaluateToResultAsync(string expression,
        IDictionary<string, object?>? parameters = null)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return EvaluationResult.Fail(new ExpressionError
            {
                Code = ErrorCodes.EmptyInput,
                Message = "表达式不能为空",
                Context = expression
            }, stopwatch.Elapsed);
        }

        var trimmed = expression.Trim();
        var processed = PreprocessExpression(trimmed);

        if (!CanExecute(processed))
        {
            return EvaluationResult.Fail(new ExpressionError
            {
                Code = ErrorCodes.InvalidSyntax,
                Message = "表达式无效或包含不可用的函数",
                Context = expression
            }, stopwatch.Elapsed);
        }

        try
        {
            var result = await EvaluateDecimalAsync(processed, parameters);
            stopwatch.Stop();
            return EvaluationResult.Ok(result, result?.ToString(), stopwatch.Elapsed);
        }
        catch (OverflowException)
        {
            _logger.Information("Decimal overflow, fallback to double: {Expression}", processed);
            try
            {
                var result = await EvaluateDoubleAsync(processed, parameters);
                stopwatch.Stop();
                return EvaluationResult.Ok(result, result?.ToString(), stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                _logger.Error(e, "Failed to evaluate with double: {Expression}", processed);
                return EvaluationResult.Fail(new ExpressionError
                {
                    Code = ErrorCodes.EvaluationFailed,
                    Message = e.Message,
                    Context = expression,
                    InnerException = e
                }, stopwatch.Elapsed);
            }
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            _logger.Error(e, "Failed to evaluate: {Expression}", processed);
            return EvaluationResult.Fail(new ExpressionError
            {
                Code = ErrorCodes.EvaluationFailed,
                Message = e.Message,
                Context = expression,
                InnerException = e
            }, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 异步计算表达式（向后兼容，返回旧版结果类型）
    /// </summary>
    public Task<ExpressionResult> EvaluateAsync(string expression)
    {
        return EvaluateAsync(expression, null);
    }

    /// <summary>
    /// 异步计算表达式（向后兼容，带参数）
    /// </summary>
    public async Task<ExpressionResult> EvaluateAsync(string expression, IDictionary<string, object?>? parameters)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionResult.Fail(expression ?? "", "表达式不能为空");

        var trimmed = expression.Trim();
        var processed = PreprocessExpression(trimmed);

        if (!CanExecute(processed))
            return ExpressionResult.Fail(expression, "表达式无效或包含不可用的函数");

        try
        {
            var result = await EvaluateDecimalAsync(processed, parameters);
            return ExpressionResult.Ok(expression, result);
        }
        catch (OverflowException)
        {
            _logger.Information("Decimal overflow, fallback to double: {Expression}", processed);
            try
            {
                var result = await EvaluateDoubleAsync(processed, parameters);
                return ExpressionResult.Ok(expression, result);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to evaluate with double: {Expression}", processed);
                return ExpressionResult.Fail(expression, e.Message);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to evaluate: {Expression}", processed);
            return ExpressionResult.Fail(expression, e.Message);
        }
    }

    /// <summary>
    /// 预处理表达式
    /// </summary>
    private string PreprocessExpression(string expression)
    {
        return PercentRegex.Replace(expression, match =>
        {
            var number = decimal.Parse(match.Groups[1].Value);
            return (number / 100m).ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
    }

    private async Task<object?> EvaluateDecimalAsync(string expression, IDictionary<string, object?>? parameters)
    {
        return await EvaluateCoreAsync(
            expression,
            parameters,
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions | ExpressionOptions.DecimalAsDefault);
    }

    private async Task<object?> EvaluateDoubleAsync(string expression, IDictionary<string, object?>? parameters)
    {
        return await EvaluateCoreAsync(
            expression,
            parameters,
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
    }

    private async Task<object?> EvaluateCoreAsync(
        string expression,
        IDictionary<string, object?>? parameters,
        ExpressionOptions options)
    {
        var expr = new NCalc.Expression(expression)
        {
            Options = options
        };
        expr.EvaluateFunction += HandleFunction;
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                expr.Parameters[key] = value;
            }
        }

        if (expr.HasErrors())
            throw new InvalidOperationException(expr.Error?.Message ?? "表达式语法错误");

        var result = await expr.EvaluateAsync();
        if (!IsValidResult(result))
            throw new InvalidOperationException("表达式结果不是有效的计算值");

        return result;
    }

    /// <summary>
    /// 检查计算结果是否有效（排除集合类型）
    /// </summary>
    private bool IsValidResult(object? result)
    {
        if (result == null) return true;

        if (result is System.Collections.IEnumerable && result is not string)
            return false;

        return true;
    }

    private void HandleFunction(string name, FunctionEventArgs args)
    {
        var engine = _scriptEngine;
        if (engine == null || !engine.CanInvoke(name))
            return;

        var parameters = Enumerable.Range(0, args.Parameters.Count)
            .Select(args.Parameters.Evaluate)
            .ToArray();
        args.Result = engine.Invoke(name, parameters);
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
            if (_ownsScriptEngine && _scriptEngine != null)
            {
                _scriptEngine.Dispose();
            }
        }

        _disposed = true;
    }

    ~ExpressionEvaluator()
    {
        Dispose(false);
    }

    #endregion
}