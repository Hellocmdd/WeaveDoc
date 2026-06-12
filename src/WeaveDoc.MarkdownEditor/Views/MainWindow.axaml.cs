using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using WeaveDoc.MarkdownEditor.ViewModels;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window, IMarkdownEditorHost
    {
        private NativeMarkdownEditorControl? _nativeEditor;
        private PreviewWebViewControl? _previewWebView;
        private PdfViewerControl? _pdfViewer;
        private string _lastPdfFilePath = string.Empty;
        private string? _temporaryPdfFilePath;
        public string? InitialFilePath { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
        }

        public string PreviewHtml =>
            DataContext is MainWindowViewModel vm ? vm.PreviewHtml : string.Empty;

        private bool _isPdfViewerInitialized = false;

        private async void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                // ESC 键退出全屏
                if (_pdfViewer != null && _pdfViewer.IsFullScreen)
                {
                    await _pdfViewer.ToggleFullScreen();
                    e.Handled = true;
                }
            }
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            _nativeEditor = GetNativeEditor();
            _previewWebView = this.FindControl<PreviewWebViewControl>("PreviewWebView");
            _pdfViewer = this.FindControl<PdfViewerControl>("PdfViewer");

            if (DataContext is MainWindowViewModel vm)
            {
                ApplyViewModelContentToEditorIfNotEmpty();
                _previewWebView?.SetContent(vm.PreviewHtml);
                UpdatePreviewPaneVisibility(vm);
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }

            // Wire up live preview: editor changes → debounced preview refresh
            if (_nativeEditor != null)
            {
                _nativeEditor.ContentEdited += NativeEditor_ContentEdited;
            }

            if (!string.IsNullOrEmpty(InitialFilePath))
            {
                await OpenFileFromPathAsync(InitialFilePath);
            }

        }

        private void NativeEditor_ContentEdited(object? sender, EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && _nativeEditor != null)
            {
                // Sync editor content to ViewModel before refreshing preview
                vm.EditorContent = _nativeEditor.GetContent();
                _ = vm.DebouncedRefreshPreview();
            }
        }

        public async Task OpenFileFromPathAsync(string filePath)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider != null)
            {
                var file = await storageProvider.TryGetFileFromPathAsync(filePath);
                if (file != null)
                {
                    if (filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        await OpenPdfStorageFileAsync(file);
                    }
                    else
                    {
                        await OpenMarkdownStorageFileAsync(file);
                    }
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is MainWindowViewModel vm)
            {
                if (e.PropertyName == nameof(MainWindowViewModel.PreviewHtml) && _previewWebView != null)
                {
                    _previewWebView.SetContent(vm.PreviewHtml);
                    UpdatePreviewPaneVisibility(vm);
                }
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private NativeMarkdownEditorControl? GetNativeEditor() =>
            _nativeEditor ??= this.FindControl<NativeMarkdownEditorControl>("NativeEditor");

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
            {
                vm.ApplyOpenedMarkdown(result);

                if (result.Succeeded)
                {
                    ApplyViewModelContentToEditor();
                    UpdatePreviewPaneVisibility(vm);
                }
            }

            return result;
        }

        private async void OpenFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenMarkdownFileAsync();
        }

        public async Task SaveMarkdownFileAsync()
        {
            SyncLiveEditorContent();

            if (DataContext is MainWindowViewModel vm && !string.IsNullOrEmpty(vm.CurrentFilePath))
            {
                vm.SaveFile(vm.CurrentFilePath);
            }
            else
            {
                await SaveMarkdownFileAsAsync();
            }
        }

        private void SyncLiveEditorContent()
        {
            if (GetNativeEditor() is { } nativeEditor && DataContext is MainWindowViewModel vm)
            {
                var content = nativeEditor.GetContent();
                vm.EditorContent = content;
                nativeEditor.SetContent(content);
            }
        }

        private void ApplyViewModelContentToEditorIfNotEmpty()
        {
            if (DataContext is MainWindowViewModel vm && string.IsNullOrEmpty(vm.EditorContent))
                return;

            ApplyViewModelContentToEditor();
        }

        private void ApplyViewModelContentToEditor()
        {
            if (GetNativeEditor() is { } nativeEditor && DataContext is MainWindowViewModel vm)
            {
                nativeEditor.SetContent(vm.EditorContent);
            }
        }

        private void UpdatePreviewPaneVisibility(MainWindowViewModel? viewModel = null)
        {
            viewModel ??= DataContext as MainWindowViewModel;

            var layoutGrid = this.FindControl<Grid>("MarkdownEditorLayoutGrid");
            var editorPane = this.FindControl<Border>("EditorPane");
            var previewPane = this.FindControl<Border>("PreviewPane");
            if (layoutGrid?.ColumnDefinitions.Count < 2 || previewPane == null)
                return;

            var hasPreview = !string.IsNullOrWhiteSpace(viewModel?.PreviewHtml);
            
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log($"[DIAG] UpdatePreviewPaneVisibility: hasPreview={hasPreview}, PreviewHtml.Length={viewModel?.PreviewHtml?.Length ?? 0}");

            layoutGrid.ColumnDefinitions[1].Width = hasPreview
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
            previewPane.IsVisible = hasPreview;

            if (editorPane != null)
                editorPane.Margin = hasPreview ? new Thickness(0, 0, 4, 0) : new Thickness(0);
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

        private async void SaveFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsync();
        }

        private async void PreviewToggle_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SyncLiveEditorContent();

            if (DataContext is MainWindowViewModel vm)
            {
                vm.RefreshPreview();
                WeaveDoc.MarkdownEditor.Helpers.Logger.Log($"[DIAG] PreviewToggle: PreviewHtml length={vm.PreviewHtml.Length}");

                _previewWebView?.SetContent(vm.PreviewHtml);

                UpdatePreviewPaneVisibility(vm);

                if (_previewWebView != null)
                {
                    WeaveDoc.MarkdownEditor.Helpers.Logger.Log("[DIAG] PreviewToggle: calling Activate...");
                    await _previewWebView.Activate(false);
                    WeaveDoc.MarkdownEditor.Helpers.Logger.Log("[DIAG] PreviewToggle: Activate returned.");
                }
            }
        }

        private async void SaveAsFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsAsync();
        }

        private void BoldButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("**", "**");
        }

        private void ItalicButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("*", "*");
        }

        private void UnderlineButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("<u>", "</u>");
        }

        private void H1Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("# ", "");
        }

        private void H2Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("## ", "");
        }

        private void H3Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("### ", "");
        }

        private void BulletListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("- ", "");
        }

        private void NumberedListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("1. ", "");
        }

        private void CodeBlockButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("```\n", "\n```");
        }

        private void LinkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("[链接文本](", ")");
        }

        private void ImageButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("![图片描述](", ")");
        }

        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            if (_nativeEditor != null)
            {
                _nativeEditor.InsertAtCursor(prefix, suffix);
            }
        }

        public void ScrollPreviewToSelection(int startLine, int startCol, int endLine, int endCol)
        {
            if (_previewWebView != null)
            {
                _previewWebView.ScrollToSelection(startLine, startCol, endLine, endCol);
            }
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



        private async void OpenPdfFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenPdfFileAsync();
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

        private async Task ShowPdfViewer(string filePath, string displayName, bool isTemporary)
        {
            var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
            var mainTabControl = this.FindControl<TabControl>("MainTabControl");
            var pdfFileName = this.FindControl<TextBlock>("PdfFileName");

            if (pdfTabItem != null && mainTabControl != null && pdfFileName != null)
            {
                ReplaceTemporaryPdfFile(isTemporary ? filePath : null);
                _lastPdfFilePath = filePath;
                pdfFileName.Text = string.IsNullOrWhiteSpace(displayName)
                    ? Path.GetFileName(filePath)
                    : displayName;

                if (DataContext is MainWindowViewModel vm)
                    vm.SetStatus($"已打开 PDF：{pdfFileName.Text}");
                
                // 先切换到PDF标签页
                mainTabControl.SelectedItem = pdfTabItem;
                
                // 等待标签切换完成
                await Task.Delay(50);
                
                // 然后激活PDF控件并加载PDF
                _pdfViewer ??= this.FindControl<PdfViewerControl>("PdfViewer");
                if (_pdfViewer != null)
                {
                    await _pdfViewer.Activate();
                    await _pdfViewer.LoadPdfAsync(filePath);

                    if (_pdfViewer.IsUsingFallback && DataContext is MainWindowViewModel pdfFailureVm)
                        pdfFailureVm.SetStatus(_pdfViewer.FallbackStatusText, isError: true);
                }
            }
        }

        private void ShowPdfOpenFailure(string message)
        {
            var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
            var mainTabControl = this.FindControl<TabControl>("MainTabControl");
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

        protected override void OnClosed(EventArgs e)
        {
            ReplaceTemporaryPdfFile(null);
            base.OnClosed(e);
        }

        private void ClosePdf_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var pdfFileName = this.FindControl<TextBlock>("PdfFileName");
            var mainTabControl = this.FindControl<TabControl>("MainTabControl");

            if (pdfFileName != null)
            {
                pdfFileName.Text = string.Empty;
            }

            if (mainTabControl != null)
            {
                mainTabControl.SelectedIndex = 0;
            }
        }

        private async void FullScreenPdf_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_pdfViewer != null)
            {
                await _pdfViewer.ToggleFullScreen();
            }
        }

        private async void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var mainTabControl = sender as TabControl;
            if (mainTabControl == null) return;

            var selectedTab = mainTabControl.SelectedItem as TabItem;
            if (selectedTab == null) return;

            if (selectedTab.Header?.ToString() == "Markdown Editor")
                {
                    // 先确保 PDF 完全隐藏（使用异步版本）
                    if (_pdfViewer != null)
                    {
                        await _pdfViewer.DeactivateAsync();
                    }

                    if (_previewWebView != null)
                    {
                        // 强制刷新预览内容，确保 data-pos 属性正确加载
                        if (DataContext is MainWindowViewModel vm)
                        {
                            _previewWebView.SetContent(vm.PreviewHtml);
                            UpdatePreviewPaneVisibility(vm);
                        }
                    }
                }
            else if (selectedTab.Header?.ToString() == "PDF Reader")
                {
                    if (_previewWebView != null)
                    {
                        _previewWebView.Deactivate();
                    }
                    
                    if (_pdfViewer != null)
                    {
                        if (!_isPdfViewerInitialized)
                        {
                            _isPdfViewerInitialized = true;
                            await _pdfViewer.InitializeAsync();
                        }
                        else if (!string.IsNullOrEmpty(_lastPdfFilePath))
                        {
                            await _pdfViewer.Activate();
                        }
                    }
                }
        }
    }
}
