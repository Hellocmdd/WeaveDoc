using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.App.Views;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App;

public partial class App : Application
{
    private readonly ConfigManager _configManager;
    private readonly DocumentConversionEngine _engine;

    public App() : this(null!, null!) { }

    public App(ConfigManager configManager, DocumentConversionEngine engine)
    {
        _configManager = configManager;
        _engine = engine;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_configManager, _engine);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
