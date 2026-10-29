using Expression.Services;
using Ti.Avalonia.Shadcn.Controls;

namespace Expression.Views;

public partial class PluginWebView : ShadWindow
{
    private bool isInitialized;

    public PluginWebView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;

        var componentContext = ExpressionRegister.Instance.ComponentContext;
        var plugin = componentContext.Resolve<ExpressionPlugin>();
        var expressionService = componentContext.Resolve<ExpressionJsService>();
        var hotKeyCaptureService = componentContext.Resolve<HotKeyCaptureJsService>();
        var services = new ServiceCollection();
        services.AddSingleton(expressionService);
        services.AddSingleton(hotKeyCaptureService);
        var serviceProvider = services.BuildServiceProvider();

        await WebView.EnsureCoreWebView2Async();
        await WebView.InitializeBridgeAsync(services, serviceProvider);
        
#if DEBUG
        WebView.Source = new Uri("http://localhost:5003");
#else
            var indexHtml = Path.Combine(plugin.Context.PluginPath, "ui", "dist", "index.html");
            WebView.Source = new Uri(indexHtml);
#endif
        
    }
}
