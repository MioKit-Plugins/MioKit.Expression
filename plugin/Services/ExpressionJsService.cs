using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Expression.Interfaces;
using Expression.Models;
using MioKit.Webview2.Core;
using Serilog;

namespace Expression.Services;

[JsService("ExpressionService")]
public partial class ExpressionJsService : ObservableObject, IDisposable
{
    private readonly IScriptEngineFactory scriptEngineFactory;
    private readonly ExpressionPlugin plugin;
    private readonly ExpressionScriptLifecycle scriptLifecycle;
    private readonly ILogger logger;
    private bool lastGlobalCalculateEnabled;

    public ExpressionJsService(
        IScriptEngineFactory scriptEngineFactory,
        ExpressionPlugin plugin,
        ExpressionScriptLifecycle scriptLifecycle)
        : this(scriptEngineFactory, plugin, scriptLifecycle, plugin.Context.Logger) { }

    internal ExpressionJsService(
        IScriptEngineFactory scriptEngineFactory,
        ExpressionPlugin plugin,
        ExpressionScriptLifecycle scriptLifecycle,
        ILogger logger)
    {
        this.scriptEngineFactory = scriptEngineFactory;
        this.plugin = plugin;
        this.scriptLifecycle = scriptLifecycle;
        this.logger = logger.ForContext<ExpressionJsService>();
        lastGlobalCalculateEnabled = GlobalCalculateEnabled;
        plugin.PropertyChanged += OnPluginPropertyChanged;
    }

    // Both the host's configuration form and the WebView edit this same EAV.
    // Keep it out of settings.json and the script editor's pending save snapshot.
    public bool GlobalCalculateEnabled
    {
        get => plugin.GetAllowCalculateWithoutAttach() == true;
        set
        {
            if (GlobalCalculateEnabled != value)
                plugin.SetAllowCalculateWithoutAttach(value);
        }
    }

    private void OnPluginPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var enabled = GlobalCalculateEnabled;
        if (lastGlobalCalculateEnabled == enabled) return;
        lastGlobalCalculateEnabled = enabled;
        OnPropertyChanged(nameof(GlobalCalculateEnabled));
    }

    void IDisposable.Dispose() => plugin.PropertyChanged -= OnPluginPropertyChanged;

    [ObservableProperty]
    private bool isScriptRunning;

    [ObservableProperty]
    private string availableFunctionsText = string.Empty;

    public event EventHandler<ExpressionLogEventArgs>? LogEmitted;

    public ExpressionSettingsDto GetSettings()
    {
        var settings = ExpressionSettings.Store.Current;
        return new ExpressionSettingsDto
        {
            DecimalPrecision = settings.DecimalPrecision,
            UseThousandsSeparator = settings.UseThousandsSeparator,
            ScriptEnabled = settings.ScriptEnabled
        };
    }

    public string GetSavedScript()
    {
        return ExpressionSettings.ScriptContent;
    }

    public string GetBuiltInScript()
    {
        return ExpressionSettings.GetBuiltInScript();
    }

    public async Task<CompileResult> CompileScript(string code)
    {
        try
        {
            using var evaluator = new DebugExpressionEvaluator(scriptEngineFactory);
            evaluator.ConsoleOutput += OnConsoleOutput;
            var success = await evaluator.RestartAsync(code);
            IsScriptRunning = success;
            AvailableFunctionsText = success
                ? string.Join(", ", evaluator.AvailableFunctions.OrderBy(static f => f))
                : string.Empty;

            EmitLog(success ? "info" : "error", success ? "脚本编译成功" : "脚本编译失败");

            return new CompileResult
            {
                Success = success,
                Message = success ? "脚本编译成功" : "脚本编译失败",
                Functions = evaluator.AvailableFunctions.OrderBy(static f => f).ToArray()
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to compile expression script");
            IsScriptRunning = false;
            AvailableFunctionsText = string.Empty;
            EmitLog("error", ex.Message);
            return new CompileResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<ApplyResult> ApplyScript(string code, int decimalPrecision, bool useThousandsSeparator, bool scriptEnabled)
    {
        try
        {
            var change = ExpressionSettings.Apply(code, decimalPrecision, useThousandsSeparator, scriptEnabled);
            var reload = change?.ScriptChanged == true
                ? scriptLifecycle.WaitForPendingReloadAsync()
                : scriptLifecycle.EnsureCurrentScriptAsync();
            if (!await reload)
            {
                return new ApplyResult { Success = false, Message = "设置已保存，但脚本加载失败，请修正脚本后重试" };
            }

            return new ApplyResult
            {
                Success = true,
                Message = "设置已保存"
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to apply expression settings");
            return new ApplyResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<CalculateResult> Calculate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new CalculateResult
            {
                Success = false,
                Error = "表达式不能为空"
            };
        }

        try
        {
            using var evaluator = new DebugExpressionEvaluator(scriptEngineFactory);
            evaluator.ConsoleOutput += OnConsoleOutput;
            if (ExpressionSettings.ScriptEnabled)
            {
                await evaluator.StartAsync(ExpressionSettings.GetFullScriptContent());
            }
            var result = await evaluator.EvaluateAsync(expression);
            if (!result.Success)
            {
                EmitLog("error", result.ErrorMessage ?? "计算失败");
                return new CalculateResult
                {
                    Success = false,
                    Error = result.ErrorMessage
                };
            }

            var formattedResult = ExpressionSettings.FormatResult(result.Value);
            EmitLog("info", $"{expression} = {formattedResult}");
            return new CalculateResult
            {
                Success = true,
                Result = formattedResult
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to calculate expression: {Expression}", expression);
            EmitLog("error", ex.Message);
            return new CalculateResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private void OnConsoleOutput(object? sender, ConsoleOutputEventArgs e)
    {
        var level = e.Level switch
        {
            ConsoleOutputLevel.Warn => "warning",
            ConsoleOutputLevel.Error => "error",
            ConsoleOutputLevel.Debug => "debug",
            _ => "info"
        };

        EmitLog(level, e.Message, e.Timestamp);
    }

    private void EmitLog(string level, string message, DateTime? timestamp = null)
    {
        LogEmitted?.Invoke(this, new ExpressionLogEventArgs
        {
            Level = level,
            Message = message,
            Timestamp = timestamp ?? DateTime.Now
        });
    }

}
