using Avalonia.Controls;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Views;

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

        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        var ragTab = this.FindControl<RagTab>("RagTabControl");
        if (ragTab != null)
        {
            await ragTab.InitializeAsync();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var ragTab = this.FindControl<RagTab>("RagTabControl");
        ragTab?.Dispose();
    }
}
