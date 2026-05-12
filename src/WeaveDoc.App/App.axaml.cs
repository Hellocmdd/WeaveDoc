using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.App.Views;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter;

namespace WeaveDoc.App;

public class App : Application
{
    private ConfigManager? _configManager;
    private DocumentConversionEngine? _engine;

    public App() { }

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
        if (_configManager != null
            && _engine != null
            && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_configManager, _engine);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
