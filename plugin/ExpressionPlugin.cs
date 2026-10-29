using Expression.Views;

namespace Expression;

public class ExpressionPlugin : PluginBase, IExpressionPluginFeature, IPluginMainWindowFeature,
    ISearchableFeature, IExpressionEntryFeature, IInvocationPresentationFeature
{
    private readonly Action showMainWindow;

    public ExpressionPlugin()
        : this(() => WindowManager.ShowUniqueWindow<PluginWebView>(ExpressionConst.PluginId))
    {
    }

    internal ExpressionPlugin(Action showMainWindow) : base(ExpressionConst.PluginId)
    {
        this.showMainWindow = showMainWindow;
        Name = "表达式计算";
        Description = "打开表达式脚本工作台";
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            ExpressionExtension.AllowCalculateWithoutAttachProperty,
        ];
    }

    public override Guid MioType => ExpressionConst.PluginType;

    /// <summary>
    /// 加载固定 ID 的表达式搜索作用域与计算节点，使 / 入口、command 模式和全局搜索在挂树后生效。
    /// </summary>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureTreeLoadedAsync<ExpressionCalcScope>(ExpressionConst.CalcScopeId);
        await EnsureTreeLoadedAsync<ExpressionCalcNode>(ExpressionConst.CalcNodeId);
    }

    public override ValueTask<PluginDataCleanupPlan> CreateDataCleanupPlanAsync(
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PluginDataCleanupPlan
        {
            // 仅填写未挂在插件根节点下的额外持久化根 ID。
            AdditionalEavRootObjectIds = []
        });
    }

    public void OpenPluginMainWindow()
    {
        showMainWindow();
    }

    public ValueTask SearchAsync(SearchRequest request, IWriteOnlyResultList<SearchResult> writeOnlyResultList,
        CancellationToken cancellationToken)
    {
        if (request.IsEmptySearchText) return ValueTask.CompletedTask;
        if (!this.TryMatch(request.SearchText!, out var result, out var title))
        {
            return ValueTask.CompletedTask;
        }

        writeOnlyResultList.Add(new SearchResult(request, this)
        {
            Title = title,
            Description = Description,
            MatchResult = result!.Value,
            PinMode = SortPriority.Normal,
            Source = "表达式计算",
        });

        return ValueTask.CompletedTask;
    }

    public Task InvokeAsync(InvokeContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        OpenPluginMainWindow();
        return Task.CompletedTask;
    }

    public InvocationPresentation GetPresentation(InvocationPresentationRequest request) => new()
    {
        Interaction = InvocationInteraction.CompleteBeforeExecution
    };
}
