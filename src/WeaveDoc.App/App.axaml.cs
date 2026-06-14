using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.App.Views;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App;

public partial class App : Application
{
    private readonly ConfigManager _configManager;
    private readonly DocumentConversionEngine _engine;
    private readonly LocalAiService _aiService;

    public App() : this(null!, null!, null!) { }

    public App(ConfigManager configManager, DocumentConversionEngine engine, LocalAiService aiService)
    {
        _configManager = configManager;
        _engine = engine;
        _aiService = aiService;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_configManager, _engine, _aiService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
