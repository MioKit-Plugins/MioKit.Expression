namespace Expression.Models;

public class ExpressionCalcContext
{
    private readonly Lazy<Task<CalculateResult>> calculation;

    public ExpressionCalcContext(
        string expression,
        string? clipboardArgs,
        Func<Task<ExpressionResult>> evaluate,
        Func<object?, string> format)
    {
        Expression = expression;
        ClipboardArgs = clipboardArgs;
        // The calculation belongs to the result, not its recycled preview control.
        // Each consumer cancels only its own wait, so hiding the preview cannot
        // cancel a pending Enter/copy operation or run a random function twice.
        calculation = new Lazy<Task<CalculateResult>>(() => Task.Run(async () =>
        {
            try
            {
                var result = await evaluate().ConfigureAwait(false);
                return result.Success
                    ? new CalculateResult { Success = true, Result = format(result.Value) }
                    : new CalculateResult { Error = result.ErrorMessage ?? "计算失败" };
            }
            catch (Exception ex)
            {
                return new CalculateResult { Error = ex.Message };
            }
        }));
    }

    public string Expression { get; }
    public string? ClipboardArgs { get; }

    public Task<CalculateResult> GetResultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return calculation.Value.WaitAsync(cancellationToken);
    }
}
