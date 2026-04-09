using Avalonia.Controls;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow() { }

    public MainWindow(ConfigManager configManager, DocumentConversionEngine engine)
    {
        InitializeComponent();

        var templateTab = this.FindControl<TemplateTab>("TemplateTabControl");
        templateTab?.SetConfigManager(configManager);

        var convertTab = this.FindControl<ConvertTab>("ConvertTabControl");
        convertTab?.SetServices(configManager, engine);
    }
}
