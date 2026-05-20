using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.ViewModels;

namespace WeaveDoc.MarkdownEditor.Views;

public partial class MarkdownEditorTab : UserControl, IMarkdownEditorHost
{
    private MonacoEditorControl? _monacoEditor;
    private PreviewWebViewControl? _previewWebView;
    private PdfViewerControl? _pdfViewer;
    private bool _isMonacoReady;
    private (int line, int column)? _pendingScrollRequest;
    private string _lastPdfFilePath = string.Empty;

    public MarkdownEditorTab()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    public string PreviewHtml =>
        DataContext is MainWindowViewModel vm ? vm.PreviewHtml : string.Empty;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pdfViewer is { IsFullScreen: true })
        {
            await _pdfViewer.ToggleFullScreen();
            e.Handled = true;
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _monacoEditor = this.FindControl<MonacoEditorControl>("MonacoEditor");
        _previewWebView = this.FindControl<PreviewWebViewControl>("PreviewWebView");
        _pdfViewer = this.FindControl<PdfViewerControl>("PdfViewer");

        if (DataContext is MainWindowViewModel vm)
        {
            _monacoEditor?.SetContentAsync(vm.EditorContent);
            _previewWebView?.SetContent(vm.PreviewHtml);
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
            return;

        if (e.PropertyName == nameof(MainWindowViewModel.EditorContent))
            _monacoEditor?.SetContentAsync(vm.EditorContent);

        if (e.PropertyName == nameof(MainWindowViewModel.PreviewHtml))
            _previewWebView?.SetContent(vm.PreviewHtml);
    }

    public async Task OpenMarkdownFileAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 Markdown 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown 文件") { Patterns = ["*.md", "*.markdown", "*.txt"] },
                FilePickerFileTypes.All
            ]
        });

        var filePath = selected.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(filePath) && DataContext is MainWindowViewModel vm)
            vm.OpenFile(filePath);
    }

    public async Task SaveMarkdownFileAsync()
    {
        if (DataContext is MainWindowViewModel vm && !string.IsNullOrWhiteSpace(vm.CurrentFilePath))
        {
            vm.SaveFile(vm.CurrentFilePath);
            return;
        }

        await SaveMarkdownFileAsAsync();
    }

    public async Task SaveMarkdownFileAsAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var selected = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存 Markdown 文件",
            DefaultExtension = "md",
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown 文件") { Patterns = ["*.md", "*.markdown"] },
                new FilePickerFileType("文本文件") { Patterns = ["*.txt"] },
                FilePickerFileTypes.All
            ]
        });

        var filePath = selected?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(filePath) && DataContext is MainWindowViewModel vm)
            vm.SaveFile(filePath);
    }

    public async Task OpenPdfFileAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 PDF 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PDF 文件") { Patterns = ["*.pdf"] },
                FilePickerFileTypes.All
            ]
        });

        var filePath = selected.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(filePath))
            await ShowPdfViewer(filePath);
    }

    public void ScrollPreviewToSelection(int startLine, int startCol, int endLine, int endCol)
    {
        _previewWebView?.ScrollToSelection(startLine, startCol, endLine, endCol);
    }

    public async void SetMonacoReady(bool ready)
    {
        _isMonacoReady = ready;

        if (ready && _pendingScrollRequest.HasValue && _monacoEditor != null)
        {
            var request = _pendingScrollRequest.Value;
            _pendingScrollRequest = null;
            await _monacoEditor.ScrollToPositionAsync(request.line, request.column);
        }
    }

    public async void ScrollEditorToPosition(int lineNumber, int column)
    {
        if (_monacoEditor != null && _isMonacoReady)
        {
            await _monacoEditor.ScrollToPositionAsync(lineNumber, column);
        }
        else if (!_isMonacoReady)
        {
            _pendingScrollRequest = (lineNumber, column);
        }
    }

    public async void ScrollEditorToPositionWithRange(int lineNumber, int column, int selectionLength)
    {
        if (_monacoEditor != null && _isMonacoReady)
        {
            await _monacoEditor.ScrollToPositionAsync(lineNumber, column, selectionLength);
        }
        else if (!_isMonacoReady)
        {
            _pendingScrollRequest = (lineNumber, column);
        }
    }

    public async void ClearEditorHighlight()
    {
        if (_monacoEditor != null && _isMonacoReady)
            await _monacoEditor.ClearHighlightAsync();
    }

    public async Task ActivateAsync()
    {
        var innerTabs = this.FindControl<TabControl>("MarkdownEditorInnerTabs");
        if (innerTabs?.SelectedItem is TabItem { Header: "PDF Reader" })
        {
            _monacoEditor?.Deactivate();
            _previewWebView?.Deactivate();

            if (!string.IsNullOrEmpty(_lastPdfFilePath) && _pdfViewer != null)
                await _pdfViewer.Activate();

            return;
        }

        if (_pdfViewer != null)
            await _pdfViewer.DeactivateAsync();

        if (_monacoEditor != null)
            await _monacoEditor.Activate(false);

        if (_previewWebView != null)
            await _previewWebView.Activate(false);
    }

    public async Task DeactivateAsync()
    {
        _monacoEditor?.Deactivate();
        _previewWebView?.Deactivate();

        if (_pdfViewer != null && _pdfViewer.IsFullScreen)
            await _pdfViewer.ToggleFullScreen();

        if (_pdfViewer != null)
            await _pdfViewer.DeactivateAsync();
    }

    private async Task ShowPdfViewer(string filePath)
    {
        var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
        var mainTabControl = this.FindControl<TabControl>("MarkdownEditorInnerTabs");
        var pdfFileName = this.FindControl<TextBlock>("PdfFileName");

        if (pdfTabItem == null || mainTabControl == null || pdfFileName == null)
            return;

        _lastPdfFilePath = filePath;
        pdfFileName.Text = Path.GetFileName(filePath);
        mainTabControl.SelectedItem = pdfTabItem;

        await Task.Delay(50);
        if (_pdfViewer != null)
        {
            await _pdfViewer.Activate();
            await _pdfViewer.LoadPdfAsync(filePath);
        }
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e) =>
        await OpenMarkdownFileAsync();

    private async void SaveFile_Click(object sender, RoutedEventArgs e) =>
        await SaveMarkdownFileAsync();

    private async void SaveAsFile_Click(object sender, RoutedEventArgs e) =>
        await SaveMarkdownFileAsAsync();

    private async void OpenPdfFile_Click(object sender, RoutedEventArgs e) =>
        await OpenPdfFileAsync();

    private async void FullScreenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_pdfViewer != null)
            await _pdfViewer.ToggleFullScreen();
    }

    private async void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl mainTabControl)
            return;

        if (mainTabControl.SelectedItem is not TabItem selectedTab)
            return;

        if (selectedTab.Header?.ToString() == "Markdown Editor")
        {
            if (_pdfViewer != null)
                await _pdfViewer.DeactivateAsync();

            await Task.Delay(150);

            if (_monacoEditor != null)
                await _monacoEditor.Activate(false);

            if (_previewWebView != null)
            {
                _previewWebView.SetOnPreviewReadyCallback(async () =>
                {
                    if (_monacoEditor != null)
                        await _monacoEditor.RequestCurrentSelectionAsync();
                });

                await _previewWebView.Activate(false);
                if (DataContext is MainWindowViewModel vm)
                    _previewWebView.SetContent(vm.PreviewHtml);
            }
        }
        else if (selectedTab.Header?.ToString() == "PDF Reader")
        {
            _monacoEditor?.Deactivate();
            _previewWebView?.Deactivate();

            await Task.Delay(100);

            if (!string.IsNullOrEmpty(_lastPdfFilePath) && _pdfViewer != null)
                await _pdfViewer.Activate();
        }
    }
}
