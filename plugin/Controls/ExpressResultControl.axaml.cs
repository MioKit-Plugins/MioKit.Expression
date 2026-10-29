using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Expression.Models;
using Expression.Services;

namespace Expression.Controls;

public partial class ExpressResultControl : UserControl
{
    private readonly ExpressionPreviewController preview;

    public ExpressResultControl()
    {
        preview = new ExpressionPreviewController(Dispatch, value =>
        {
            Expression = value.Expression;
            ExpressionResult = value.Result;
            IsLoading = value.IsLoading;
            HasError = value.Error is not null;
            ErrorMessage = value.Error;
        });
        InitializeComponent();
    }

    public static readonly StyledProperty<string?> ExpressionProperty =
        AvaloniaProperty.Register<ExpressResultControl, string?>(nameof(Expression));
    public string? Expression
    {
        get => GetValue(ExpressionProperty);
        set => SetValue(ExpressionProperty, value);
    }

    public static readonly StyledProperty<string?> ExpressionResultProperty =
        AvaloniaProperty.Register<ExpressResultControl, string?>(nameof(ExpressionResult));
    public string? ExpressionResult
    {
        get => GetValue(ExpressionResultProperty);
        set => SetValue(ExpressionResultProperty, value);
    }

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ExpressResultControl, bool>(nameof(IsLoading));
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ExpressResultControl, bool>(nameof(HasError));
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ExpressResultControl, string?>(nameof(ErrorMessage));
    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        preview?.Show(GetContext());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        preview.Activate(GetContext());
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        preview.Deactivate();
        base.OnDetachedFromVisualTree(e);
    }

    private ExpressionCalcContext? GetContext() => (DataContext as SearchResult)?.Payload as ExpressionCalcContext;

    private static void Dispatch(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }
}
