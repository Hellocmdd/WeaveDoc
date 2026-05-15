using Avalonia;
using Avalonia.Markup.Xaml;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Ui.Views;

namespace WeaveDoc.Converter.Ui;

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
        if (_configManager != null && _engine != null)
        {
            var window = new MainWindow(_configManager, _engine);
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
