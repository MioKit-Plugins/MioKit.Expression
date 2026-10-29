using System.Text.Json;
using Expression.Models;
using Expression.Services;
using Xunit;

namespace Expression.Tests;

public sealed class ExpressionSettingsStoreTests
{
    [Fact]
    public void BatchValidatesEverythingAndSavesAndNotifiesOnlyOnce()
    {
        var saved = new List<ExpressionSettingsSnapshot>();
        var notifications = new List<SettingChangedEventArgs>();
        var store = new ExpressionSettingsStore(() => new(), saved.Add);
        store.Changed += (_, e) => notifications.Add(e);
        var next = new ExpressionSettingsSnapshot
        {
            DecimalPrecision = 0, UseThousandsSeparator = true, ScriptEnabled = false, ScriptContent = "new script"
        };
        var change = store.Update(_ => next);
        Assert.Equal(next, Assert.Single(saved));
        Assert.Same(change, Assert.Single(notifications));
        Assert.True(change!.ScriptChanged);
        Assert.Equal(next, store.Current);
        Assert.Null(store.Update(_ => next));
        Assert.Single(saved);
        Assert.Single(notifications);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public void InvalidPrecisionCannotPartiallyApplyOtherFields(int precision)
    {
        var saves = 0;
        var events = 0;
        var store = new ExpressionSettingsStore(() => new(), _ => saves++);
        store.Changed += (_, _) => events++;
        var original = store.Current;
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(_ => new()
        {
            DecimalPrecision = precision, UseThousandsSeparator = true, ScriptEnabled = false, ScriptContent = "new script"
        }));
        Assert.Same(original, store.Current);
        Assert.Equal(0, saves);
        Assert.Equal(0, events);
    }

    [Fact]
    public void OversizedScriptAndSaveFailureLeaveStateAndNotificationsUntouched()
    {
        var events = 0;
        var store = new ExpressionSettingsStore(() => new(), _ => throw new IOException("disk failure"));
        store.Changed += (_, _) => events++;
        var original = store.Current;
        Assert.Throws<ArgumentException>(() => store.Update(s => s with
        {
            DecimalPrecision = 0,
            ScriptContent = new string('x', ExpressionSettings.Constraints.MaxScriptContentLength + 1)
        }));
        Assert.Throws<IOException>(() => store.Update(s => s with { ScriptContent = "new script" }));
        Assert.Same(original, store.Current);
        Assert.Equal(0, events);
        Assert.Equal(0, store.Read().Revision);
    }

    [Fact]
    public void AtomicSavePreservesExistingSchemaAndOldFileWhenReplacementFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "miokit-expression-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        try
        {
            // The existing four-field settings.json remains readable without migration.
            File.WriteAllText(path, """{"ScriptEnabled":false,"ScriptContent":"legacy","DecimalPrecision":3,"UseThousandsSeparator":true}""");
            var store = new ExpressionSettingsStore(path);
            Assert.Equal("legacy", store.Current.ScriptContent);
            Assert.False(store.Current.ScriptEnabled);
            store.Update(s => s with { ScriptContent = "updated", DecimalPrecision = 0 });
            Assert.Equal(store.Current, new ExpressionSettingsStore(path).Current);
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                Assert.Equal(4, json.RootElement.EnumerateObject().Count());
            var content = File.ReadAllText(path);
            var original = store.Current;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var error = Record.Exception(() => store.Update(s => s with { ScriptContent = "must not publish" }));
                Assert.True(error is IOException or UnauthorizedAccessException);
            }
            Assert.Same(original, store.Current);
            Assert.Equal(content, File.ReadAllText(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task ConcurrentUpdatesDoNotLoseStateAndRevisionsAreMonotonic()
    {
        var saved = new List<ExpressionSettingsSnapshot>();
        var revisions = new System.Collections.Concurrent.ConcurrentBag<long>();
        var store = new ExpressionSettingsStore(() => new() { DecimalPrecision = 0 }, saved.Add);
        store.Changed += (_, e) => revisions.Add(e.Revision);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
            store.Update(s => s with { DecimalPrecision = s.DecimalPrecision + 1 }))));
        Assert.Equal(12, store.Current.DecimalPrecision);
        Assert.Equal(12, saved.Count);
        Assert.Equal(Enumerable.Range(1, 12).Select(i => (long)i), revisions.Order());
    }
}
