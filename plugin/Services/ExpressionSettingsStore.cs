using System.Text.Json;
using Expression.Models;

namespace Expression.Services;

internal sealed class ExpressionSettingsStore
{
    private readonly object sync = new();
    private readonly Func<ExpressionSettingsSnapshot> load;
    private readonly Action<ExpressionSettingsSnapshot> save;
    private ExpressionSettingsSnapshot? current;
    private long revision;

    internal ExpressionSettingsStore(string path, Action<Exception>? logWarning = null)
        : this(() => Load(path, logWarning), value => Save(path, value)) { }

    internal ExpressionSettingsStore(Func<ExpressionSettingsSnapshot> load, Action<ExpressionSettingsSnapshot> save)
    {
        this.load = load;
        this.save = save;
    }

    public event EventHandler<SettingChangedEventArgs>? Changed;
    public ExpressionSettingsSnapshot Current => Read().Settings;

    public (ExpressionSettingsSnapshot Settings, long Revision) Read()
    {
        lock (sync) return (current ??= load(), revision);
    }

    public SettingChangedEventArgs? Update(Func<ExpressionSettingsSnapshot, ExpressionSettingsSnapshot> update)
    {
        SettingChangedEventArgs change;
        lock (sync)
        {
            var oldValue = current ??= load();
            var newValue = update(oldValue);
            Validate(newValue);
            if (oldValue == newValue) return null;
            // Publish the new in-memory state only after the complete file has
            // been persisted; a failed save leaves the previous snapshot intact.
            save(newValue);
            current = newValue;
            change = new SettingChangedEventArgs(oldValue, newValue, ++revision);
        }

        Changed?.Invoke(this, change);
        return change;
    }

    private static void Validate(ExpressionSettingsSnapshot value)
    {
        var precision = ExpressionSettings.ValidateDecimalPrecision(value.DecimalPrecision);
        if (!precision.IsValid) throw new ArgumentOutOfRangeException(nameof(value.DecimalPrecision), precision.ErrorMessage);
        var script = ExpressionSettings.ValidateScriptContent(value.ScriptContent);
        if (!script.IsValid) throw new ArgumentException(script.ErrorMessage, nameof(value.ScriptContent));
        ArgumentNullException.ThrowIfNull(value.ScriptContent);
    }

    private static ExpressionSettingsSnapshot Load(string path, Action<Exception>? logWarning)
    {
        try
        {
            var value = File.Exists(path)
                ? JsonSerializer.Deserialize<ExpressionSettingsSnapshot>(File.ReadAllText(path)) ?? new()
                : new();
            Validate(value);
            return value;
        }
        catch (Exception ex)
        {
            logWarning?.Invoke(ex);
            return new();
        }
    }

    private static void Save(string path, ExpressionSettingsSnapshot value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
