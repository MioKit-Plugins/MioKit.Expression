using Expression.Controls;
using Expression.Interfaces;
using Expression.Models;
using Expression.Services;
using Serilog;

namespace Expression;

[EavType(ExpressionConst.CalcNodeTypeId)]
public sealed class ExpressionCalcNode : MioObject, ISearchableFeature, IInvokeFeature, IMemoryNodeFeature,
    IInvocationPresentationFeature
{
    private readonly Func<bool> isCalcScopeAttached;
    private readonly Func<bool> isGlobalCalculateEnabled;
    private readonly IExpressionParser? parser;
    private readonly SearchExpressionEvaluator? evaluator;
    private readonly ILogger? logger;
    private readonly Func<object?, string>? formatResult;
    private readonly Action<string> copyResult;
    private readonly Action<string> showError;

    public ExpressionCalcNode(string id)
        : this(id, IsCalcScopeAttached, IsGlobalCalculateEnabled, null, null, null)
    {
    }

    internal ExpressionCalcNode(
        string id,
        Func<bool> isCalcScopeAttached,
        Func<bool> isGlobalCalculateEnabled,
        IExpressionParser? parser,
        SearchExpressionEvaluator? evaluator,
        ILogger? logger,
        Func<object?, string>? formatResult = null,
        Action<string>? copyResult = null,
        Action<string>? showError = null) : base(id)
    {
        this.isCalcScopeAttached = isCalcScopeAttached;
        this.isGlobalCalculateEnabled = isGlobalCalculateEnabled;
        this.parser = parser;
        this.evaluator = evaluator;
        this.logger = logger;
        this.formatResult = formatResult;
        this.copyResult = copyResult ?? CopyToClipboard;
        this.showError = showError ?? ShowError;
        Name = "表达式计算";
        Description = "计算表达式并复制结果";
    }

    public override Guid MioType => ExpressionConst.CalcNodeType;

    public InvocationPresentation GetPresentation(InvocationPresentationRequest request) => new()
    {
        Interaction = InvocationInteraction.CompleteBeforeExecution
    };

    public ValueTask SearchAsync(
        SearchRequest request,
        IWriteOnlyResultList<SearchResult> writeOnlyResultList,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attached = isCalcScopeAttached();
        if (!attached && !isGlobalCalculateEnabled())
        {
            GetLogger().Debug("Global expression search skipped, attach required: {SearchText}", request.SearchText);
            return ValueTask.CompletedTask;
        }

        var clipboard = request.Context.Clipboard.Text;
        var fromClipboard = string.IsNullOrWhiteSpace(request.SearchText);
        if (fromClipboard && string.IsNullOrWhiteSpace(clipboard))
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            var expressionParser = parser ?? ExpressionRegister.Instance.ComponentContext.Resolve<IExpressionParser>();
            var expressionEvaluator = evaluator ?? ExpressionRegister.Instance.ComponentContext.Resolve<SearchExpressionEvaluator>();
            var parseResult = expressionParser.Parse(request.SearchText, clipboard);
            if (!parseResult.Success || string.IsNullOrEmpty(parseResult.Expression))
            {
                GetLogger().Debug("Expression parse failed: {SearchText}", request.SearchText);
                return ValueTask.CompletedTask;
            }

            var expression = parseResult.Expression;
            if (!expressionParser.IsLikelyExpression(expression))
            {
                GetLogger().Debug("Input is not a likely expression: {Expression}", expression);
                return ValueTask.CompletedTask;
            }

            if (!expressionEvaluator.CanExecute(expression))
            {
                GetLogger().Debug("Expression cannot execute: {Expression}", expression);
                return ValueTask.CompletedTask;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var clipboardArgs = clipboard?.Trim();
            Dictionary<string, object?>? parameters = string.IsNullOrWhiteSpace(clipboardArgs)
                ? null
                : new() { [ParameterResolver.ClipboardParamName] = clipboardArgs };
            var calculation = new ExpressionCalcContext(
                expression, clipboardArgs,
                () => expressionEvaluator.EvaluateAsync(expression, parameters),
                formatResult ?? ExpressionSettings.FormatResult);

            writeOnlyResultList.Add(new SearchResult(request, this)
            {
                Title = expression,
                Description = fromClipboard ? "计算剪贴板表达式并复制结果" : "计算表达式并复制结果",
                Payload = calculation,
                PinMode = SortPriority.AlwaysTop,
                Source = "表达式计算",
                RenderControlFunc = static () => new ExpressResultControl()
            });
            GetLogger().Debug("Expression search result listed: {Expression} {Attached}", expression, attached);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GetLogger().Error(ex, "Expression search failed: {SearchText}", request.SearchText);
        }

        return ValueTask.CompletedTask;
    }

    public async Task InvokeAsync(InvokeContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.SearchResult?.Payload is not ExpressionCalcContext resultContext)
        {
            showError("没有可复制的计算结果");
            return;
        }

        try
        {
            var result = await resultContext.GetResultAsync(context.CancellationToken);
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!result.Success)
            {
                GetLogger().Warning("Expression evaluation failed: {Expression} {Error}", resultContext.Expression, result.Error);
                showError($"计算失败：{result.Error}");
                return;
            }

            copyResult(result.Result);
            GetLogger().Information(
                "Copied expression result: {Expression} {Result}",
                resultContext.Expression,
                result.Result);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            GetLogger().Error(ex, "Failed to copy expression result: {Expression}", resultContext.Expression);
            showError($"复制结果失败：{ex.Message}");
        }
    }

    private static void CopyToClipboard(string text)
    {
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    private static void ShowError(string message)
    {
        ExpressionRegister.Instance.ComponentContext.Resolve<IToastService>().Show(message, ToastLevel.Error);
    }

    private static bool IsCalcScopeAttached()
    {
        return ExpressionRegister.Instance.ComponentContext.Resolve<ISearchBoxWindow>().AttachedPanel is ExpressionCalcScope;
    }

    private static bool IsGlobalCalculateEnabled()
    {
        var plugin = ExpressionRegister.Instance.ComponentContext.Resolve<ExpressionPlugin>();
        return plugin.GetAllowCalculateWithoutAttach() == true;
    }

    private ILogger GetLogger()
    {
        return logger ?? ExpressionRegister.Instance.ComponentContext
            .Resolve<ExpressionPlugin>()
            .Context.Logger
            .ForContext<ExpressionCalcNode>();
    }
}
