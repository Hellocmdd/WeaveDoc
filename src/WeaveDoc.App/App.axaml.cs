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
    private readonly ILiteratureRepository _literatureRepository;

    public App() : this(null!, null!, null!, null!) { }

    public App(ConfigManager configManager, DocumentConversionEngine engine, LocalAiService aiService, ILiteratureRepository literatureRepository)
    {
        _configManager = configManager;
        _engine = engine;
        _aiService = aiService;
        _literatureRepository = literatureRepository;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_configManager, _engine, _aiService, _literatureRepository, autoInitializeRag: true);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
