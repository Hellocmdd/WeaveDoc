using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Views;

public partial class MainWindow : Window
{
    private readonly RagTabViewModel _viewModel;

    public MainWindow() : this(null!, null!) { }

    public MainWindow(ConfigManager? configManager, DocumentConversionEngine? engine)
    {
        InitializeComponent();
        _viewModel = new RagTabViewModel();
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;

        if (configManager != null && engine != null)
        {
            ConvertTabControl.SetServices(configManager, engine);
            TemplateTabControl.SetConfigManager(configManager);
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.SendAsync();
    }

    private void OnToggleDocumentsClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleDocumentPanel();
    }

    private async void OnRefreshCorpusClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshCorpusAsync();
    }

    private async void OnPickDocumentClick(object? sender, RoutedEventArgs e)
    {
        var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要导入的文档",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown / Text / JSON")
                {
                    Patterns = ["*.md", "*.txt", "*.json"]
                }
            ]
        });

        var localPath = selected.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            await _viewModel.AddDocumentFromPathAsync(localPath);
        }
    }

    private void OnClearConversationClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ClearConversation();
    }

    private async void OnAddDocumentClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.AddDocumentAsync();
    }

    private async void OnDeleteDocumentClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.DeleteSelectedDocumentAsync();
    }

    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            await _viewModel.SendAsync();
        }
    }

    private void OnSelectDocumentsTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectedPanelTab = 0;
    }

    private void OnSelectSettingsTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectedPanelTab = 1;
    }

    private void OnSelectLocalProviderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChatProvider = "llama_server";
    }

    private void OnSelectCloudProviderClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ChatProvider = "cloud";
    }

    private void OnSaveCloudSettingsClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SaveCloudSettings();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
