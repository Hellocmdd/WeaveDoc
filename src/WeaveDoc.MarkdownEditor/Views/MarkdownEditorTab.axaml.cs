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
using WeaveDoc.MarkdownEditor.Services;
using WeaveDoc.MarkdownEditor.ViewModels;

namespace WeaveDoc.MarkdownEditor.Views;

public partial class MarkdownEditorTab : UserControl, IMarkdownEditorHost
{
    private NativeMarkdownEditorControl? _nativeEditor;
    private PreviewWebViewControl? _previewWebView;
    private PdfViewerControl? _pdfViewer;
    private string _lastPdfFilePath = string.Empty;
    private string? _temporaryPdfFilePath;

    public MarkdownEditorTab()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
    }

    public string PreviewHtml =>
        DataContext is MainWindowViewModel vm ? vm.PreviewHtml : string.Empty;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private NativeMarkdownEditorControl? GetNativeEditor() =>
        _nativeEditor ??= this.FindControl<NativeMarkdownEditorControl>("NativeEditor");

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
        _nativeEditor = GetNativeEditor();
        _previewWebView = this.FindControl<PreviewWebViewControl>("PreviewWebView");
        _pdfViewer = this.FindControl<PdfViewerControl>("PdfViewer");

        if (DataContext is MainWindowViewModel vm)
        {
            _nativeEditor?.SetContent(vm.EditorContent);
            _previewWebView?.SetContent(vm.PreviewHtml);
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        ReplaceTemporaryPdfFile(null);
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm)
            return;

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

        var file = selected.FirstOrDefault();
        if (file == null)
            return;

        await OpenMarkdownStorageFileAsync(file);
    }

    public async Task<MarkdownFileOpenResult> OpenMarkdownStorageFileAsync(IStorageFile file)
    {
        var result = await StorageFileOpenService.OpenMarkdownAsync(file).ConfigureAwait(true);
        if (DataContext is MainWindowViewModel vm)
            vm.ApplyOpenedMarkdown(result);

        return result;
    }

    public async Task SaveMarkdownFileAsync()
    {
        SyncLiveEditorContent();

        if (DataContext is MainWindowViewModel vm && !string.IsNullOrWhiteSpace(vm.CurrentFilePath))
        {
            vm.SaveFile(vm.CurrentFilePath);
            return;
        }

        await SaveMarkdownFileAsAsync();
    }

    public async Task SaveMarkdownFileAsAsync()
    {
        SyncLiveEditorContent();

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

    private void SyncLiveEditorContent()
    {
        if (GetNativeEditor() is { } nativeEditor && DataContext is MainWindowViewModel vm)
            vm.EditorContent = nativeEditor.GetContent();
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

        var file = selected.FirstOrDefault();
        if (file == null)
            return;

        await OpenPdfStorageFileAsync(file);
    }

    public async Task<PdfFileOpenResult> OpenPdfStorageFileAsync(IStorageFile file)
    {
        var result = await StorageFileOpenService.PreparePdfAsync(file).ConfigureAwait(true);
        if (result.Succeeded)
            await ShowPdfViewer(result.FilePath, result.DisplayName, result.IsTemporary);
        else
            ShowPdfOpenFailure(result.ErrorMessage ?? "打开 PDF 文件失败。");

        return result;
    }

    public void ScrollPreviewToSelection(int startLine, int startCol, int endLine, int endCol)
    {
        _previewWebView?.ScrollToSelection(startLine, startCol, endLine, endCol);
    }

    public void SetMonacoReady(bool ready)
    {
    }

    public void ScrollEditorToPosition(int lineNumber, int column)
    {
        GetNativeEditor()?.ScrollToPosition(lineNumber, column);
    }

    public void ScrollEditorToPositionWithRange(int lineNumber, int column, int selectionLength)
    {
        GetNativeEditor()?.ScrollToPosition(lineNumber, column, selectionLength);
    }

    public void ClearEditorHighlight()
    {
        var nativeEditor = GetNativeEditor();
        var selection = nativeEditor?.GetSelection();
        if (selection.HasValue)
            nativeEditor?.SetSelection(selection.Value.Start, 0);
    }

    public async Task ActivateAsync()
    {
        var innerTabs = this.FindControl<TabControl>("MarkdownEditorInnerTabs");
        if (innerTabs?.SelectedItem is TabItem { Header: "PDF Reader" })
        {
            _previewWebView?.Deactivate();

            if (!string.IsNullOrEmpty(_lastPdfFilePath) && _pdfViewer != null)
                await _pdfViewer.Activate();

            return;
        }

        if (_pdfViewer != null)
            await _pdfViewer.DeactivateAsync();

        if (_previewWebView != null && DataContext is MainWindowViewModel vm)
            _previewWebView.SetContent(vm.PreviewHtml);
    }

    public async Task DeactivateAsync()
    {
        _previewWebView?.Deactivate();

        if (_pdfViewer != null && _pdfViewer.IsFullScreen)
            await _pdfViewer.ToggleFullScreen();

        if (_pdfViewer != null)
            await _pdfViewer.DeactivateAsync();
    }

    private async Task ShowPdfViewer(string filePath, string displayName, bool isTemporary)
    {
        var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
        var mainTabControl = this.FindControl<TabControl>("MarkdownEditorInnerTabs");
        var pdfFileName = this.FindControl<TextBlock>("PdfFileName");

        if (pdfTabItem == null || mainTabControl == null || pdfFileName == null)
            return;

        ReplaceTemporaryPdfFile(isTemporary ? filePath : null);
        _lastPdfFilePath = filePath;
        pdfFileName.Text = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(filePath)
            : displayName;

        if (DataContext is MainWindowViewModel vm)
            vm.SetStatus($"已打开 PDF：{pdfFileName.Text}");

        mainTabControl.SelectedItem = pdfTabItem;

        await Task.Delay(50);
        _pdfViewer ??= this.FindControl<PdfViewerControl>("PdfViewer");
        if (_pdfViewer != null)
        {
            await _pdfViewer.Activate();
            await _pdfViewer.LoadPdfAsync(filePath);

            if (_pdfViewer.IsUsingFallback && DataContext is MainWindowViewModel pdfFailureVm)
                pdfFailureVm.SetStatus(_pdfViewer.FallbackStatusText, isError: true);
        }
    }

    private void ShowPdfOpenFailure(string message)
    {
        var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
        var mainTabControl = this.FindControl<TabControl>("MarkdownEditorInnerTabs");
        var pdfFileName = this.FindControl<TextBlock>("PdfFileName");

        if (pdfFileName != null)
            pdfFileName.Text = "PDF 未打开";

        if (pdfTabItem != null && mainTabControl != null)
            mainTabControl.SelectedItem = pdfTabItem;

        if (_pdfViewer != null)
        {
            _pdfViewer.FallbackStatusText = message;
            _pdfViewer.IsUsingFallback = true;
        }

        if (DataContext is MainWindowViewModel vm)
            vm.SetStatus(message, isError: true);
    }

    private void ReplaceTemporaryPdfFile(string? filePath)
    {
        if (!string.Equals(_temporaryPdfFilePath, filePath, StringComparison.Ordinal))
            StorageFileOpenService.TryDeleteTemporaryFile(_temporaryPdfFilePath);

        _temporaryPdfFilePath = filePath;
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

            if (_previewWebView != null)
            {
                if (DataContext is MainWindowViewModel vm)
                    _previewWebView.SetContent(vm.PreviewHtml);
            }
        }
        else if (selectedTab.Header?.ToString() == "PDF Reader")
        {
            _previewWebView?.Deactivate();

            await Task.Delay(100);

            if (!string.IsNullOrEmpty(_lastPdfFilePath) && _pdfViewer != null)
                await _pdfViewer.Activate();
        }
    }
}
