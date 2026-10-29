using System.Globalization;
using Expression.Models;
using Expression.Services;

namespace Expression;

public sealed class SettingChangedEventArgs(
    ExpressionSettingsSnapshot oldValue,
    ExpressionSettingsSnapshot newValue,
    long revision) : EventArgs
{
    public ExpressionSettingsSnapshot OldValue { get; } = oldValue;
    public ExpressionSettingsSnapshot NewValue { get; } = newValue;
    public long Revision { get; } = revision;
    public bool ScriptChanged => OldValue.ScriptEnabled != NewValue.ScriptEnabled || OldValue.ScriptContent != NewValue.ScriptContent;
}

public record SettingValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static SettingValidationResult Success() => new() { IsValid = true };
    public static SettingValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}

public static class ExpressionSettings
{
    private static readonly object SyncRoot = new();
    private static ExpressionSettingsStore? store;

    public static event EventHandler<SettingChangedEventArgs>? SettingChanged
    {
        add => Store.Changed += value;
        remove => Store.Changed -= value;
    }

    public static class Keys
    {
        public const string ScriptEnabled = "Expression.ScriptEnabled";
        public const string ScriptContent = "Expression.ScriptContent";
        public const string DecimalPrecision = "Expression.DecimalPrecision";
        public const string UseThousandsSeparator = "Expression.UseThousandsSeparator";
    }

    public static class Defaults
    {
        public const bool ScriptEnabled = true;
        public const string ScriptContent = "";
        public const int DecimalPrecision = 10;
        public const bool UseThousandsSeparator = false;
    }

    public static class Constraints
    {
        public const int MinDecimalPrecision = 0;
        public const int MaxDecimalPrecision = 28;
        public const int MaxScriptContentLength = 1024 * 1024;
    }

    public static bool ScriptEnabled
    {
        get => Current.ScriptEnabled;
        set => Store.Update(s => s with { ScriptEnabled = value });
    }

    public static string ScriptContent
    {
        get => Current.ScriptContent;
        set
        {
            var validation = ValidateScriptContent(value);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage, nameof(value));
            }

            Store.Update(s => s with { ScriptContent = value });
        }
    }

    public static int DecimalPrecision
    {
        get => Current.DecimalPrecision;
        set
        {
            var validation = ValidateDecimalPrecision(value);
            if (!validation.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(value), validation.ErrorMessage);
            }

            Store.Update(s => s with { DecimalPrecision = value });
        }
    }

    public static bool UseThousandsSeparator
    {
        get => Current.UseThousandsSeparator;
        set => Store.Update(s => s with { UseThousandsSeparator = value });
    }

    public static SettingValidationResult ValidateDecimalPrecision(int value)
    {
        if (value < Constraints.MinDecimalPrecision)
        {
            return SettingValidationResult.Failure($"DecimalPrecision must be at least {Constraints.MinDecimalPrecision}");
        }

        if (value > Constraints.MaxDecimalPrecision)
        {
            return SettingValidationResult.Failure($"DecimalPrecision must be at most {Constraints.MaxDecimalPrecision}");
        }

        return SettingValidationResult.Success();
    }

    public static SettingValidationResult ValidateScriptContent(string? value)
    {
        if (value is { Length: > Constraints.MaxScriptContentLength })
        {
            return SettingValidationResult.Failure($"ScriptContent must be at most {Constraints.MaxScriptContentLength} characters");
        }

        return SettingValidationResult.Success();
    }

    public static string GetFullScriptContent()
    {
        return GetFullScriptContent(Current);
    }

    public static string GetBuiltInScript()
    {
        return """
               window.expression = Object.assign(window.expression || {}, {
                   add: (...args) => args.reduce((sum, val) => sum + val, 0),
                   multiply: (...args) => args.reduce((product, val) => product * val, 1),
                   avg: (...args) => args.length === 0 ? 0 : args.reduce((sum, val) => sum + val, 0) / args.length,
                   maximum: (...args) => Math.max(...args),
                   minimum: (...args) => Math.min(...args),
                   factorial: (n) => {
                       if (n < 0) return NaN;
                       let result = 1;
                       for (let i = 2; i <= n; i++) result *= i;
                       return result;
                   },
                   fib: (n) => {
                       if (n < 0) return NaN;
                       if (n <= 1) return n;
                       let a = 0, b = 1;
                       for (let i = 2; i <= n; i++) [a, b] = [b, a + b];
                       return b;
                   },
                   isPrime: (n) => {
                       if (n < 2) return false;
                       if (n === 2) return true;
                       if (n % 2 === 0) return false;
                       for (let i = 3; i <= Math.sqrt(n); i += 2) {
                           if (n % i === 0) return false;
                       }
                       return true;
                   },
                   toBase: (num, base) => num.toString(base),
                   fromBase: (str, base) => parseInt(str, base),
                   roundTo: (num, decimals) => {
                       const factor = Math.pow(10, decimals);
                       return Math.round(num * factor) / factor;
                   },
                   percent: (value, total) => (value / total) * 100,
                   random: (min, max) => Math.random() * (max - min) + min,
                   randomInt: (min, max) => Math.floor(Math.random() * (max - min + 1)) + min
               });
               """;
    }

    public static string FormatNumber(decimal value) => FormatResult(value);
    public static string FormatNumber(double value) => FormatResult(value);

    public static string FormatResult(object? value)
    {
        var settings = Current;
        return ExpressionResultFormatter.Format(value, settings.DecimalPrecision, settings.UseThousandsSeparator, CultureInfo.CurrentCulture);
    }

    public static SettingChangedEventArgs? Apply(string code, int decimalPrecision, bool useThousandsSeparator, bool scriptEnabled) =>
        Store.Update(_ => new ExpressionSettingsSnapshot
        {
            ScriptContent = code ?? string.Empty,
            DecimalPrecision = decimalPrecision,
            UseThousandsSeparator = useThousandsSeparator,
            ScriptEnabled = scriptEnabled
        });

    public static string GetFullScriptContent(ExpressionSettingsSnapshot settings)
    {
        var builtIn = GetBuiltInScript();
        return string.IsNullOrWhiteSpace(settings.ScriptContent)
            ? builtIn
            : $"{builtIn}{Environment.NewLine}{Environment.NewLine}// 用户自定义脚本{Environment.NewLine}{settings.ScriptContent}";
    }

    private static ExpressionSettingsSnapshot Current => Store.Current;

    internal static ExpressionSettingsStore Store
    {
        get
        {
            lock (SyncRoot)
            {
                if (store is not null) return store;
                var plugin = ExpressionRegister.Instance.ComponentContext.Resolve<ExpressionPlugin>();
                store = new ExpressionSettingsStore(
                    Path.Combine(plugin.Context.PluginDataPath, "settings.json"),
                    ex => plugin.Context.Logger.Warning(ex, "Failed to load expression settings"));
                return store;
            }
        }
    }
}
