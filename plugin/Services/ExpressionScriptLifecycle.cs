using Expression.Interfaces;
using Expression.Models;
using Serilog;

namespace Expression.Services;

public sealed class ExpressionScriptLifecycle : IPluginLifecycleComponent
{
    private readonly object sync = new();
    private readonly IScriptEngine engine;
    private readonly Func<ExpressionSettingsStore> getStore;
    private readonly ILogger logger;
    private ExpressionSettingsStore? store;
    private Task<bool> pending = Task.FromResult(true);
    private bool running;
    private long generation;
    private long latestRevision;

    public ExpressionScriptLifecycle(SearchExpressionEvaluator searchEvaluator, ExpressionPlugin plugin)
        : this(searchEvaluator.SearchEngine, () => ExpressionSettings.Store, plugin.Context.Logger) { }

    internal ExpressionScriptLifecycle(IScriptEngine engine, Func<ExpressionSettingsStore> getStore, ILogger logger)
    {
        this.engine = engine;
        this.getStore = getStore;
        this.logger = logger.ForContext<ExpressionScriptLifecycle>();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<bool> initial;
        lock (sync)
        {
            if (!running)
            {
                store = getStore();
                running = true;
                ++generation;
                store.Changed += OnSettingChanged;
                var snapshot = store.Read();
                latestRevision = snapshot.Revision;
                QueueReload(snapshot.Settings, generation, cancellationToken);
            }
            initial = pending;
        }
        try
        {
            await initial.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task<bool> stopping;
        lock (sync)
        {
            if (store is not null) store.Changed -= OnSettingChanged;
            running = false;
            ++generation;
            var previous = pending;
            stopping = pending = Task.Run(async () =>
            {
                await DrainAsync(previous).ConfigureAwait(false);
                await engine.StopAsync().ConfigureAwait(false);
                return true;
            });
        }
        // Stop drains an active reload before stopping the engine. Already queued
        // reloads see the new generation and cannot restart it after shutdown.
        // The engine API cannot interrupt a running transition. Drain it even
        // when the caller cancels, so the host cannot dispose an active engine.
        await stopping.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal Task<bool> WaitForPendingReloadAsync()
    {
        lock (sync) return pending;
    }

    internal Task<bool> EnsureCurrentScriptAsync()
    {
        lock (sync)
        {
            // Pressing Apply again after a failed reload retries the same saved
            // script without another disk write or a duplicate settings event.
            if (running && pending.IsCompletedSuccessfully && !pending.Result)
                QueueReload(store!.Current, generation, CancellationToken.None);
            return pending;
        }
    }

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (!e.ScriptChanged) return;
        lock (sync)
        {
            // Notifications are emitted outside the settings lock. Reject an
            // older notification if concurrent applies delivered them out of order.
            if (!running || e.Revision <= latestRevision) return;
            latestRevision = e.Revision;
            QueueReload(e.NewValue, generation, CancellationToken.None);
        }
    }

    private void QueueReload(ExpressionSettingsSnapshot settings, long requestGeneration, CancellationToken token)
    {
        var previous = pending;
        pending = Task.Run(async () =>
        {
            await DrainAsync(previous).ConfigureAwait(false);
            lock (sync)
            {
                if (!running || generation != requestGeneration) return true;
            }
            token.ThrowIfCancellationRequested();
            try
            {
                if (!settings.ScriptEnabled)
                {
                    await engine.StopAsync().ConfigureAwait(false);
                    logger.Information("Expression script feature is disabled");
                    return true;
                }

                var success = await engine.RestartAsync(ExpressionSettings.GetFullScriptContent(settings)).ConfigureAwait(false);
                if (success) logger.Information("Expression script service initialized");
                else logger.Warning("Failed to initialize expression script service");
                return success;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initializing expression script service");
                return false;
            }
        });
    }

    private static async Task DrainAsync(Task task)
    {
        // A canceled/failed earlier transition must not prevent shutdown or the
        // next valid settings snapshot from being applied.
        try { await task.ConfigureAwait(false); }
        catch { }
    }
}
