using Expression.Models;

namespace Expression.Services;

internal sealed record ExpressionPreviewUpdate(string? Expression, string? Result, bool IsLoading, string? Error);

/// <summary>Owns preview identity and checks it again when dispatched UI work actually runs.</summary>
internal sealed class ExpressionPreviewController(
    Action<Action> dispatch,
    Action<ExpressionPreviewUpdate> update)
{
    private ExpressionCalcContext? current;
    private CancellationTokenSource? waitCancellation;
    private long version;
    private bool active = true;

    public void Show(ExpressionCalcContext? context)
    {
        if (!active || ReferenceEquals(current, context) && waitCancellation is not null) return;
        CancelWait();
        current = context;
        var requestVersion = ++version;
        if (context is null)
        {
            Publish(requestVersion, new(null, null, false, null));
            return;
        }

        waitCancellation = new CancellationTokenSource();
        Publish(requestVersion, new(context.Expression, null, true, null));
        _ = ObserveAsync(context, requestVersion, waitCancellation.Token);
    }

    public void Activate(ExpressionCalcContext? context)
    {
        active = true;
        Show(context);
    }

    public void Deactivate()
    {
        active = false;
        ++version;
        current = null;
        CancelWait();
    }

    private async Task ObserveAsync(ExpressionCalcContext context, long requestVersion, CancellationToken token)
    {
        try
        {
            var result = await context.GetResultAsync(token).ConfigureAwait(false);
            Publish(requestVersion, new(context.Expression, result.Success ? result.Result : "Error", false, result.Error));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Publish(long requestVersion, ExpressionPreviewUpdate value) => dispatch(() =>
    {
        if (active && version == requestVersion) update(value);
    });

    private void CancelWait()
    {
        waitCancellation?.Cancel();
        waitCancellation?.Dispose();
        waitCancellation = null;
    }
}
