using System.Collections.Concurrent;
using Expression.Interfaces;
using Expression.Models;
using Expression.Services;
using Serilog;
using Xunit;

namespace Expression.Tests;

public sealed class ExpressionScriptLifecycleTests : IDisposable
{
    private readonly Serilog.Core.Logger logger = new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task BatchCausesOneReloadAndFormattingOnlyChangeCausesNone()
    {
        var saves = 0;
        var store = Store(_ => saves++);
        var engine = new RecordingEngine();
        var lifecycle = Lifecycle(engine, store);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        Assert.Single(engine.Scripts);
        store.Update(s => s with { DecimalPrecision = 0, UseThousandsSeparator = true, ScriptContent = "new script" });
        Assert.True(await lifecycle.WaitForPendingReloadAsync());
        Assert.Equal(1, saves);
        Assert.Equal(2, engine.Scripts.Count);
        Assert.EndsWith("new script", engine.Scripts.Last());

        store.Update(s => s with { DecimalPrecision = 28 });
        await lifecycle.WaitForPendingReloadAsync();
        Assert.Equal(2, engine.Scripts.Count);
        store.Update(s => s with { ScriptEnabled = false, ScriptContent = "disabled script" });
        await lifecycle.WaitForPendingReloadAsync();
        Assert.Equal(2, engine.Scripts.Count);
        Assert.Equal(1, engine.Stops);
        await lifecycle.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConcurrentAppliesReloadSeriallyAndLeaveLatestScriptActive()
    {
        var store = Store();
        var engine = new RecordingEngine();
        var lifecycle = Lifecycle(engine, store);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        var started = Signal();
        var release = Signal();
        engine.OnRestart = async script =>
        {
            if (script!.EndsWith("first"))
            {
                started.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            return true;
        };
        try
        {
            store.Update(s => s with { ScriptContent = "first" });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            store.Update(s => s with { ScriptContent = "second" });
            Assert.Equal(2, engine.Scripts.Count);
            release.TrySetResult();
            Assert.True(await lifecycle.WaitForPendingReloadAsync());
            Assert.Equal(3, engine.Scripts.Count);
            Assert.EndsWith("second", engine.Scripts.Last());
            Assert.Equal(1, engine.MaxConcurrent);
        }
        finally
        {
            release.TrySetResult();
            await lifecycle.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StopDrainsActiveReloadAndDiscardsQueuedAndFutureReloads()
    {
        var store = Store();
        var engine = new RecordingEngine();
        var lifecycle = Lifecycle(engine, store);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        var started = Signal();
        var release = Signal();
        engine.OnRestart = async _ =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return true;
        };
        store.Update(s => s with { ScriptContent = "active" });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        store.Update(s => s with { ScriptContent = "queued" });
        var stopping = lifecycle.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(stopping.IsCompleted);
        release.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(2, engine.Scripts.Count);
        Assert.Equal(1, engine.Stops);
        Assert.False(engine.IsRunning);
        store.Update(s => s with { ScriptContent = "after stop" });
        await lifecycle.WaitForPendingReloadAsync();
        Assert.Equal(2, engine.Scripts.Count);
        Assert.Equal(1, engine.MaxConcurrent);

        // Starting again loads the latest persisted snapshot and subscribes once.
        engine.OnRestart = _ => Task.FromResult(true);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        Assert.EndsWith("after stop", engine.Scripts.Last());
        await lifecycle.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FailedReloadIsObservableAndRetryDoesNotSaveAgain()
    {
        var saves = 0;
        var store = Store(_ => saves++);
        var engine = new RecordingEngine();
        var lifecycle = Lifecycle(engine, store);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        engine.OnRestart = _ => Task.FromResult(false);
        store.Update(s => s with { ScriptContent = "bad" });
        Assert.False(await lifecycle.WaitForPendingReloadAsync());
        Assert.Equal(1, saves);
        engine.OnRestart = _ => Task.FromResult(true);
        Assert.True(await lifecycle.EnsureCurrentScriptAsync());
        Assert.Equal(1, saves);
        Assert.Equal(3, engine.Scripts.Count);
        await lifecycle.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanceledStartDrainsEngineAndRemovesSettingsSubscription()
    {
        var store = Store();
        var engine = new RecordingEngine();
        var started = Signal();
        var release = Signal();
        engine.OnRestart = async _ =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            return true;
        };
        var lifecycle = Lifecycle(engine, store);
        using var cancellation = new CancellationTokenSource();
        var starting = lifecycle.StartAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        Assert.False(engine.IsRunning);
        Assert.Equal(1, engine.Stops);
        store.Update(s => s with { ScriptContent = "after canceled start" });
        await lifecycle.WaitForPendingReloadAsync();
        Assert.Single(engine.Scripts);
    }

    [Fact]
    public async Task LateOlderNotificationCannotReplaceNewerScript()
    {
        var store = Store();
        var firstEvent = Signal();
        var release = Signal();
        store.Changed += (_, e) =>
        {
            if (e.Revision == 1)
            {
                firstEvent.TrySetResult();
                release.Task.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        };
        var engine = new RecordingEngine();
        var lifecycle = Lifecycle(engine, store);
        await lifecycle.StartAsync(TestContext.Current.CancellationToken);
        var first = Task.Run(() => store.Update(s => s with { ScriptContent = "older" }), TestContext.Current.CancellationToken);
        try
        {
            await firstEvent.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            store.Update(s => s with { ScriptContent = "newer" });
            await lifecycle.WaitForPendingReloadAsync();
            release.TrySetResult();
            await first;
            await lifecycle.WaitForPendingReloadAsync();
            Assert.Equal(2, engine.Scripts.Count);
            Assert.EndsWith("newer", engine.Scripts.Last());
        }
        finally
        {
            release.TrySetResult();
            await first;
            await lifecycle.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static ExpressionSettingsStore Store(Action<ExpressionSettingsSnapshot>? save = null) =>
        new(() => new(), save ?? (_ => { }));
    private ExpressionScriptLifecycle Lifecycle(IScriptEngine engine, ExpressionSettingsStore store) =>
        new(engine, () => store, logger);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Dispose() => logger.Dispose();

    private sealed class RecordingEngine : IScriptEngine
    {
        private int active;
        private int maxConcurrent;
        private int stops;
        public ConcurrentQueue<string?> Scripts { get; } = new();
        public Func<string?, Task<bool>> OnRestart { get; set; } = _ => Task.FromResult(true);
        public int MaxConcurrent => maxConcurrent;
        public int Stops => stops;
        public string EngineId => "test";
        public bool IsRunning { get; private set; }
        public IReadOnlySet<string> AvailableFunctions { get; } = new HashSet<string>();
        public bool CanInvoke(string name) => false;
        public async Task<bool> RestartAsync(string? script = null)
        {
            Enter();
            Scripts.Enqueue(script);
            try { return IsRunning = await OnRestart(script); }
            finally { Interlocked.Decrement(ref active); }
        }
        public Task<bool> StartAsync(string? script = null) => RestartAsync(script);
        public Task StopAsync()
        {
            Enter();
            Interlocked.Increment(ref stops);
            IsRunning = false;
            Interlocked.Decrement(ref active);
            return Task.CompletedTask;
        }
        private void Enter()
        {
            var count = Interlocked.Increment(ref active);
            int previous;
            do { previous = maxConcurrent; }
            while (count > previous && Interlocked.CompareExchange(ref maxConcurrent, count, previous) != previous);
        }
        public object? Invoke(string name, params object?[] args) => throw new NotSupportedException();
        public Task<object?> InvokeAsync(string name, params object?[] args) => throw new NotSupportedException();
        public void Dispose() { }
        public event EventHandler<ScriptStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ScriptErrorEventArgs>? Error { add { } remove { } }
        public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput { add { } remove { } }
    }
}
