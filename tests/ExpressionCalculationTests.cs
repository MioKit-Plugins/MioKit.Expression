using System.Threading.Channels;
using Expression.Models;
using Expression.Services;
using Xunit;

namespace Expression.Tests;

public sealed class ExpressionCalculationTests
{
    [Fact]
    public async Task PreviewAndCopyShareOneCalculationAndCancelOnlyTheirOwnWait()
    {
        var ready = new TaskCompletionSource<ExpressionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var context = new ExpressionCalcContext("random()", null, () =>
        {
            Interlocked.Increment(ref calls);
            return ready.Task;
        }, value => value!.ToString()!);
        using var previewCancellation = new CancellationTokenSource();
        var preview = context.GetResultAsync(previewCancellation.Token);
        var copy = context.GetResultAsync(TestContext.Current.CancellationToken);
        previewCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview);
        Assert.False(copy.IsCompleted);
        ready.SetResult(ExpressionResult.Ok("random()", "42"));
        var result = await copy.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("42", result.Result);
        Assert.Same(result, await context.GetResultAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EvaluationAndFormattingFailuresBecomeSharedFailureResults()
    {
        var brokenEvaluation = new ExpressionCalcContext("broken()", null,
            () => throw new InvalidOperationException("evaluation failed"), _ => "unused");
        var brokenFormat = new ExpressionCalcContext("1+2", null,
            () => Task.FromResult(ExpressionResult.Ok("1+2", 3)),
            _ => throw new InvalidOperationException("format failed"));
        var evaluation = await brokenEvaluation.GetResultAsync(TestContext.Current.CancellationToken);
        var formatting = await brokenFormat.GetResultAsync(TestContext.Current.CancellationToken);
        Assert.False(evaluation.Success);
        Assert.Equal("evaluation failed", evaluation.Error);
        Assert.False(formatting.Success);
        Assert.Equal("format failed", formatting.Error);
    }

    [Fact]
    public async Task SameExpressionWithNewClipboardCannotReceiveOldQueuedPreview()
    {
        var queue = Channel.CreateUnbounded<Action>();
        var updates = new List<ExpressionPreviewUpdate>();
        var controller = new ExpressionPreviewController(action => queue.Writer.TryWrite(action), updates.Add);
        var firstValue = new TaskCompletionSource<ExpressionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondValue = new TaskCompletionSource<ExpressionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Context("old clipboard", firstValue.Task);
        var second = Context("new clipboard", secondValue.Task);

        controller.Show(first);
        (await Next(queue))(); // First loading state.
        firstValue.SetResult(ExpressionResult.Ok(first.Expression, "old result"));
        var oldCompletion = await Next(queue); // Queued but deliberately not rendered yet.
        controller.Show(second);
        oldCompletion();
        Assert.DoesNotContain(updates, value => value.Result == "old result");
        (await Next(queue))(); // Second loading state must clear the old result.
        Assert.True(updates[^1].IsLoading);
        Assert.Null(updates[^1].Result);
        secondValue.SetResult(ExpressionResult.Ok(second.Expression, "new result"));
        (await Next(queue))();
        Assert.Equal("new result", updates[^1].Result);
        Assert.Equal("old result", (await first.GetResultAsync(TestContext.Current.CancellationToken)).Result);
        Assert.Equal("new result", (await second.GetResultAsync(TestContext.Current.CancellationToken)).Result);
        controller.Deactivate();
    }

    [Fact]
    public async Task DetachedPreviewCanReattachAndInvalidContextClearsState()
    {
        var queue = Channel.CreateUnbounded<Action>();
        var updates = new List<ExpressionPreviewUpdate>();
        var calls = 0;
        var context = new ExpressionCalcContext("1+2", null, () =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ExpressionResult.Ok("1+2", 3));
        }, value => value!.ToString()!);
        var controller = new ExpressionPreviewController(action => queue.Writer.TryWrite(action), updates.Add);
        controller.Show(context);
        (await Next(queue))();
        var stale = await Next(queue);
        controller.Deactivate();
        stale();
        Assert.Single(updates);
        controller.Activate(context);
        (await Next(queue))();
        (await Next(queue))();
        Assert.Equal("3", updates[^1].Result);
        Assert.Equal(1, calls);
        controller.Show(null);
        (await Next(queue))();
        Assert.Equal(new ExpressionPreviewUpdate(null, null, false, null), updates[^1]);
        controller.Deactivate();
    }

    private static ExpressionCalcContext Context(string clipboard, Task<ExpressionResult> result) =>
        new("echo([clipboard])", clipboard, () => result, value => value!.ToString()!);

    private static async Task<Action> Next(Channel<Action> queue) =>
        await queue.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
}
