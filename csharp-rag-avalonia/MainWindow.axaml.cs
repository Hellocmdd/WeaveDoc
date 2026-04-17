using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RagAvalonia.ViewModels;

namespace RagAvalonia;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Opened += OnOpened;
        Closed += OnClosed;
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
