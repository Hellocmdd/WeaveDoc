using Avalonia.Controls;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow() { }

    public MainWindow(ConfigManager configManager, DocumentConversionEngine engine)
    {
        InitializeComponent();
    }
}
