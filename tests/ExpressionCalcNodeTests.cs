using System.Reflection;
using Expression.Interfaces;
using Expression.Models;
using Expression.Services;
using MioKit.Sdk;
using Serilog;
using Xunit;

namespace Expression.Tests;

[CollectionDefinition("SDK store factory", DisableParallelization = true)]
public sealed class SdkStoreFactoryCollection;

[Collection("SDK store factory")]
public sealed class ExpressionCalcNodeTests : IDisposable
{
    private readonly PropertyInfo storeFactoryProperty = typeof(MioObject).Assembly
        .GetType("MioKit.Sdk.StoreFactory", throwOnError: true)!
        .GetProperty("StoreFactoryFunc", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly object? originalStoreFactory;
    private readonly Serilog.Core.Logger logger = new LoggerConfiguration().CreateLogger();
    private readonly ExpressionParser parser = new(new ParameterResolver());
    private readonly SearchExpressionEvaluator evaluator;

    public ExpressionCalcNodeTests()
    {
        originalStoreFactory = storeFactoryProperty.GetValue(null);
        Func<MioObject, DesignMioStore> factory = owner => new DesignMioStore(owner);
        storeFactoryProperty.SetValue(null, factory);
        evaluator = new SearchExpressionEvaluator(new TestEngineFactory(logger));
    }

    [Fact]
    public async Task GlobalSearchUsesCurrentSettingAndDefaultsToDisabled()
    {
        var plugin = new ExpressionPlugin();
        Assert.False(plugin.GetAllowCalculateWithoutAttach() == true);
        var node = CreateNode(globalEnabled: () => plugin.GetAllowCalculateWithoutAttach() == true);

        Assert.Empty(await Search(node, "1+2*3"));
        Assert.Empty(await Search(node, "", "1+2*3"));
        plugin.SetAllowCalculateWithoutAttach(true);
        Assert.Single(await Search(node, "1+2*3"));
        Assert.Single(await Search(node, "", "1+2*3"));
        plugin.SetAllowCalculateWithoutAttach(false);
        Assert.Empty(await Search(node, "1+2*3"));
        Assert.Empty(await Search(node, "", "1+2*3"));
    }

    [Fact]
    public async Task WebViewAndHostConfigureSameGlobalSettingAndNotifyLiveChanges()
    {
        var plugin = new ExpressionPlugin();
        var factory = new TestEngineFactory(logger);
        var lifecycle = new ExpressionScriptLifecycle(factory.GetSearchEngine(),
            () => new ExpressionSettingsStore(() => new(), _ => { }), logger);
        using var service = new ExpressionJsService(factory, plugin, lifecycle, logger);
        var changed = new List<string?>();
        service.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var node = CreateNode(globalEnabled: () => plugin.GetAllowCalculateWithoutAttach() == true);

        Assert.False(service.GlobalCalculateEnabled);
        service.GlobalCalculateEnabled = true;
        Assert.True(plugin.GetAllowCalculateWithoutAttach());
        Assert.Single(await Search(node, "1+2"));
        Assert.Equal(new[] { nameof(ExpressionJsService.GlobalCalculateEnabled) }, changed);

        plugin.SetAllowCalculateWithoutAttach(false);
        Assert.False(service.GlobalCalculateEnabled);
        Assert.Empty(await Search(node, "1+2"));
        Assert.Equal(2, changed.Count);
        service.GlobalCalculateEnabled = false;
        plugin.Description = "unrelated change";
        Assert.Equal(2, changed.Count);

        ((IDisposable)service).Dispose();
        plugin.SetAllowCalculateWithoutAttach(true);
        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void NodePaletteDiscoversIndependentEntriesThroughFeature()
    {
        Assert.True(typeof(IExpressionEntryFeature).IsAssignableFrom(typeof(ExpressionPlugin)));
        Assert.Contains("表达式计算", NodePalette.GetCategories(typeof(ExpressionPlugin)));
        Assert.Empty(typeof(ExpressionPlugin).GetCustomAttributes<NodePaletteAttribute>());
    }

    [Theory]
    [InlineData(typeof(ExpressionCalcNode))]
    [InlineData(typeof(ExpressionCalcScope))]
    public void NonEntryNodesAreNotAdvertisedAsIndependentEntries(Type nodeType)
    {
        Assert.False(typeof(IExpressionEntryFeature).IsAssignableFrom(nodeType));
        Assert.DoesNotContain("表达式计算", NodePalette.GetCategories(nodeType));
    }

    [Fact]
    public async Task WorkbenchDeclaresPresentationAndOpensWithoutSearchBoxServices()
    {
        var openCount = 0;
        var plugin = new ExpressionPlugin(() => openCount++);
        var presentation = ((IInvocationPresentationFeature)plugin)
            .GetPresentation(new InvocationPresentationRequest());
        Assert.Equal(InvocationInteraction.CompleteBeforeExecution, presentation.Interaction);
        Assert.Equal(0, openCount);

        await plugin.InvokeAsync(new InvokeContext());
        Assert.Equal(1, openCount);
        ((IPluginMainWindowFeature)plugin).OpenPluginMainWindow();
        Assert.Equal(2, openCount);
    }

    [Fact]
    public async Task CanceledInvocationDoesNotOpenWorkbench()
    {
        var opened = false;
        var plugin = new ExpressionPlugin(() => opened = true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            plugin.InvokeAsync(new InvokeContext { CancellationToken = cancellation.Token }));
        Assert.False(opened);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AllowedSearchCalculatesTypedAndClipboardExpressions(bool attached, bool globalEnabled)
    {
        var node = CreateNode(attached, () => globalEnabled);
        foreach (var text in new string?[] { null, "", " \t", "1+2*3" })
        {
            var result = Assert.Single(await Search(node, text, " 1+2*3 "));
            Assert.Same(node, result.OwnerObject);
            Assert.Equal("1+2*3", result.Title);
            Assert.Equal(SortPriority.AlwaysTop, result.PinMode);
            Assert.NotNull(result.RenderControlFunc);
            Assert.Equal(string.IsNullOrWhiteSpace(text), result.Description!.Contains("剪贴板"));
            await AssertValue(result, 7);
        }
    }

    [Theory]
    [InlineData("3*4", "1+2", 12)]
    [InlineData("sqrt(144)", "1+2", 12)]
    [InlineData("add", "1,2,3", 6)]
    [InlineData("add", "1+2", 3)]
    [InlineData("add(4,5)", "1,2,3", 9)]
    [InlineData("3*4", "ordinary text\nmore text", 12)]
    public async Task TypedInputKeepsPrecedenceAndClipboardFunctionArguments(
        string text, string clipboard, decimal expected)
    {
        var result = Assert.Single(await Search(CreateNode(), text, clipboard));
        Assert.DoesNotContain("剪贴板", result.Description!);
        await AssertValue(result, expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("ordinary text")]
    [InlineData("123")]
    [InlineData("1e3")]
    [InlineData("0xFF")]
    [InlineData("1,2,3")]
    [InlineData("1+")]
    [InlineData("missingFunction(1)")]
    [InlineData("1+2\n3+4")]
    [InlineData("1+2\r3+4")]
    [InlineData("1+2\r\n")]
    public async Task EmptyInputIgnoresNonCalculableClipboard(string? clipboard)
    {
        Assert.Empty(await Search(CreateNode(), "", clipboard));
    }

    [Theory]
    [InlineData("ordinary text")]
    [InlineData("123")]
    [InlineData("1+")]
    [InlineData("missingFunction(1)")]
    public async Task InvalidTypedInputNeverFallsBackToClipboard(string text)
    {
        Assert.Empty(await Search(CreateNode(), text, "1+2"));
    }

    [Fact]
    public async Task ClearingInputReusesSnapshotAndClearingClipboardRemovesResult()
    {
        var node = CreateNode();
        var snapshot = Snapshot("1+2");
        var request = new SearchRequest { SearchText = "3*4", Context = snapshot };
        var typed = new ResultList();
        await node.SearchAsync(request, typed, CancellationToken.None);
        await AssertValue(Assert.Single(typed.ToList()), 12);

        // A subsequent empty search in the same open session must also work.
        var empty = new ResultList();
        await node.SearchAsync(new SearchRequest { SearchText = "", Context = snapshot }, empty, CancellationToken.None);
        await AssertValue(Assert.Single(empty.ToList()), 3);
        Assert.Empty(await Search(node, "", null));
        Assert.Empty(await Search(node, "", ""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1+2")]
    public async Task CanceledSearchDoesNotPublishResults(string? text)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var results = new ResultList();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await CreateNode().SearchAsync(Request(text, "1+2"), results, cancellation.Token));
        Assert.Empty(results.ToList());
    }

    [Fact]
    public async Task EnterCalculatesAndCopiesWithoutCreatingPreview()
    {
        var copied = new List<string>();
        var errors = new List<string>();
        var node = CreateNode(copy: copied.Add, error: errors.Add);
        var result = Assert.Single(await Search(node, "1+2*3"));
        await node.InvokeAsync(InvokeContext.FromSearchBox(result));
        Assert.Equal(new[] { "7" }, copied);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyResultRequestsSearchBoxHiddenBeforeExecutionWithoutStartingCalculation(bool clipboard)
    {
        var copied = new List<string>();
        var node = CreateNode(copy: copied.Add, error: _ => { });
        var result = Assert.Single(await Search(node, clipboard ? "" : "1+2", clipboard ? "1+2" : null));
        var provider = Assert.IsAssignableFrom<IInvocationPresentationFeature>(result.OwnerObject);
        var presentation = provider.GetPresentation(new InvocationPresentationRequest { SearchResult = result });
        Assert.Equal(InvocationInteraction.CompleteBeforeExecution, presentation.Interaction);
        Assert.Empty(copied);
        await node.InvokeAsync(InvokeContext.FromSearchBox(result));
        Assert.Equal(new[] { "3" }, copied);
    }

    [Fact]
    public async Task EnterWaitsForSharedCalculationAndCanceledPreviewDoesNotBreakCopy()
    {
        var copied = new List<string>();
        var errors = new List<string>();
        var node = CreateNode(copy: copied.Add, error: errors.Add);
        var ready = new TaskCompletionSource<ExpressionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new ExpressionCalcContext("random()", null, () => ready.Task, value => value!.ToString()!);
        var result = new SearchResult(Request("random()", null), node) { Payload = context, Title = "random()" };
        using var previewCancellation = new CancellationTokenSource();
        var preview = context.GetResultAsync(previewCancellation.Token);
        var invocation = node.InvokeAsync(InvokeContext.FromSearchBox(result));
        Assert.False(invocation.IsCompleted);
        Assert.Empty(copied);
        previewCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview);
        ready.SetResult(ExpressionResult.Ok("random()", "42"));
        await invocation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "42" }, copied);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task FailedCalculationAndClipboardWriteGiveFeedbackInsteadOfCopyingErrorText()
    {
        var copied = new List<string>();
        var errors = new List<string>();
        var node = CreateNode(copy: copied.Add, error: errors.Add);
        var context = new ExpressionCalcContext("broken()", null,
            () => Task.FromResult(ExpressionResult.Fail("broken()", "test failure")), _ => "unused");
        var result = new SearchResult(Request("broken()", null), node) { Payload = context, Title = "broken()" };
        await node.InvokeAsync(InvokeContext.FromSearchBox(result));
        Assert.Empty(copied);
        Assert.Contains("test failure", Assert.Single(errors));

        errors.Clear();
        node = CreateNode(copy: _ => throw new IOException("clipboard busy"), error: errors.Add);
        result = Assert.Single(await Search(node, "1+2"));
        await node.InvokeAsync(InvokeContext.FromSearchBox(result));
        Assert.Contains("clipboard busy", Assert.Single(errors));
    }

    [Fact]
    public async Task CanceledInvocationNeverCopiesWhenCalculationCompletesLater()
    {
        var copied = new List<string>();
        var errors = new List<string>();
        var node = CreateNode(copy: copied.Add, error: errors.Add);
        var ready = new TaskCompletionSource<ExpressionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new ExpressionCalcContext("1+2", null, () => ready.Task, value => value!.ToString()!);
        var result = new SearchResult(Request("1+2", null), node) { Payload = context, Title = "1+2" };
        using var cancellation = new CancellationTokenSource();
        var invocation = node.InvokeAsync(InvokeContext.FromSearchBox(result) with { CancellationToken = cancellation.Token });
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        ready.SetResult(ExpressionResult.Ok("1+2", 3));
        await context.GetResultAsync(TestContext.Current.CancellationToken);
        Assert.Empty(copied);
        Assert.Empty(errors);
    }

    private ExpressionCalcNode CreateNode(bool attached = false, Func<bool>? globalEnabled = null,
        Action<string>? copy = null, Action<string>? error = null) =>
        new("calc-test", () => attached, globalEnabled ?? (() => true), parser, evaluator, logger,
            value => ExpressionResultFormatterTests.Format(value), copy, error);

    private static SearchRequest Request(string? text, string? clipboard) => new()
    {
        SearchText = text,
        Context = Snapshot(clipboard)
    };

    private static InvocationSnapshot Snapshot(string? text)
    {
        // The host populates these SDK properties through internal setters.
        var clipboard = text is null ? ClipboardSnapshot.Empty : new ClipboardSnapshot();
        if (text is not null)
        {
            typeof(ClipboardSnapshot).GetProperty(nameof(ClipboardSnapshot.Text))!.SetValue(clipboard, text);
            typeof(ClipboardSnapshot).GetProperty(nameof(ClipboardSnapshot.Formats))!
                .SetValue(clipboard, new List<string> { "Text" });
        }

        var snapshot = new InvocationSnapshot();
        typeof(InvocationSnapshot).GetProperty(nameof(InvocationSnapshot.Clipboard))!.SetValue(snapshot, clipboard);
        return snapshot;
    }

    private static async Task<List<SearchResult>> Search(ExpressionCalcNode node, string? text, string? clipboard = null)
    {
        var results = new ResultList();
        await node.SearchAsync(Request(text, clipboard), results, CancellationToken.None);
        return results.ToList();
    }

    private async Task AssertValue(SearchResult result, decimal expected)
    {
        var payload = Assert.IsType<ExpressionCalcContext>(result.Payload);
        var value = await payload.GetResultAsync(TestContext.Current.CancellationToken);
        Assert.True(value.Success, value.Error);
        Assert.Equal(expected, decimal.Parse(value.Result, System.Globalization.CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        storeFactoryProperty.SetValue(null, originalStoreFactory);
        evaluator.Dispose();
        logger.Dispose();
    }

    private sealed class ResultList : IWriteOnlyResultList<SearchResult>
    {
        private readonly List<SearchResult> results = [];
        public int Count => results.Count;
        public bool IsEmpty => results.Count == 0;
        public void Add(SearchResult item) => results.Add(item);
        public void AddRange(IEnumerable<SearchResult> items) => results.AddRange(items);
        public List<SearchResult> ToList() => [..results];
    }

    // Only the custom JS function is substituted; parsing and NCalc evaluation are real.
    private sealed class TestEngineFactory(ILogger logger) : IScriptEngineFactory
    {
        private readonly IScriptEngine engine = new AddEngine();
        public ILogger Logger => logger;
        public IScriptEngine GetSearchEngine() => engine;
        public IScriptEngine CreateDebugEngine() => throw new NotSupportedException();
    }

    private sealed class AddEngine : IScriptEngine
    {
        public string EngineId => "test";
        public bool IsRunning => true;
        public IReadOnlySet<string> AvailableFunctions { get; } = new HashSet<string> { "add" };
        public bool CanInvoke(string functionName) => AvailableFunctions.Contains(functionName);
        public object? Invoke(string functionName, params object?[] args) =>
            functionName == "add" ? args.Sum(Convert.ToDecimal) : throw new NotSupportedException();
        public Task<object?> InvokeAsync(string functionName, params object?[] args) => Task.FromResult(Invoke(functionName, args));
        public Task<bool> StartAsync(string? userScript = null) => Task.FromResult(true);
        public Task<bool> RestartAsync(string? userScript = null) => Task.FromResult(true);
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
        public event EventHandler<ScriptStateChangedEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<ScriptErrorEventArgs>? Error { add { } remove { } }
        public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput { add { } remove { } }
    }
}
