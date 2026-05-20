using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        private MonacoEditorControl? _monacoEditor;
        private PreviewWebViewControl? _previewWebView;
        private PdfViewerControl? _pdfViewer;
        private bool _isMonacoReady = false;
        private (int line, int column)? _pendingScrollRequest = null;
        private string _lastPdfFilePath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
        }

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

        private void OnLoaded(object? sender, EventArgs e)
        {
            _monacoEditor = this.FindControl<MonacoEditorControl>("MonacoEditor");
            _previewWebView = this.FindControl<PreviewWebViewControl>("PreviewWebView");
            _pdfViewer = this.FindControl<PdfViewerControl>("PdfViewer");

            if (DataContext is MainWindowViewModel vm)
            {
                if (_monacoEditor != null)
                {
                    _monacoEditor.SetContentAsync(vm.EditorContent);
                }
                if (_previewWebView != null)
                {
                    _previewWebView.SetContent(vm.PreviewHtml);
                }
            }

            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is MainWindowViewModel vm)
            {
                if (e.PropertyName == nameof(MainWindowViewModel.EditorContent) && _monacoEditor != null)
                {
                    _monacoEditor.SetContentAsync(vm.EditorContent);
                }
                if (e.PropertyName == nameof(MainWindowViewModel.PreviewHtml) && _previewWebView != null)
                {
                    _previewWebView.SetContent(vm.PreviewHtml);
                }
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public async Task OpenMarkdownFileAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var dialog = new OpenFileDialog
            {
                Title = "打开 Markdown 文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Markdown 文件", Extensions = { "md", "markdown", "txt" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.OpenFile(filePath);
                }
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private async void OpenFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenMarkdownFileAsync();
        }

        public async Task SaveMarkdownFileAsync()
        {
            if (DataContext is MainWindowViewModel vm && !string.IsNullOrEmpty(vm.CurrentFilePath))
            {
                vm.SaveFile(vm.CurrentFilePath);
            }
            else
            {
                await SaveMarkdownFileAsAsync();
            }
        }

        public async Task SaveMarkdownFileAsAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var dialog = new SaveFileDialog
            {
                Title = "保存 Markdown 文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Markdown 文件", Extensions = { "md", "markdown" } },
                    new FileDialogFilter { Name = "文本文件", Extensions = { "txt" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.SaveFile(result);
                }
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private async void SaveFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsync();
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
            if (DataContext is MainWindowViewModel vm)
            {
                vm.EditorContent += prefix + suffix;
            }
        }

        public void ScrollPreviewToSelection(int startLine, int startCol, int endLine, int endCol)
        {
            Console.WriteLine($"MainWindow.ScrollPreviewToSelection called: startLine={startLine}, startCol={startCol}, endLine={endLine}, endCol={endCol}");
            Console.WriteLine($"MainWindow.ScrollPreviewToSelection: _previewWebView is null: {_previewWebView == null}");
            
            if (_previewWebView != null)
            {
                _previewWebView.ScrollToSelection(startLine, startCol, endLine, endCol);
            }
            else
            {
                Console.WriteLine("MainWindow.ScrollPreviewToSelection: _previewWebView is null, cannot scroll");
            }
        }

        public async void SetMonacoReady(bool ready)
        {
            _isMonacoReady = ready;
            Console.WriteLine($"Monaco Editor ready: {ready}");
            
            if (ready && _pendingScrollRequest.HasValue && _monacoEditor != null)
            {
                var request = _pendingScrollRequest.Value;
                _pendingScrollRequest = null;
                Console.WriteLine($"Executing pending scroll request: line={request.line}, column={request.column}");
                await _monacoEditor.ScrollToPositionAsync(request.line, request.column);
            }
        }

        public async void ScrollEditorToPosition(int lineNumber, int column)
        {
            Console.WriteLine($"ScrollEditorToPosition called: line={lineNumber}, column={column}");
            Console.WriteLine($"_monacoEditor is null: {_monacoEditor == null}");
            Console.WriteLine($"_isMonacoReady: {_isMonacoReady}");
            
            if (_monacoEditor != null && _isMonacoReady)
            {
                await _monacoEditor.ScrollToPositionAsync(lineNumber, column);
            }
            else if (!_isMonacoReady)
            {
                Console.WriteLine("Monaco Editor not ready yet, caching scroll request");
                _pendingScrollRequest = (lineNumber, column);
            }
        }

        public async void ScrollEditorToPositionWithRange(int lineNumber, int column, int selectionLength)
        {
            Console.WriteLine($"ScrollEditorToPositionWithRange called: line={lineNumber}, column={column}, length={selectionLength}");
            Console.WriteLine($"_monacoEditor is null: {_monacoEditor == null}");
            Console.WriteLine($"_isMonacoReady: {_isMonacoReady}");
            
            if (_monacoEditor != null && _isMonacoReady)
            {
                await _monacoEditor.ScrollToPositionAsync(lineNumber, column, selectionLength);
            }
            else if (!_isMonacoReady)
            {
                Console.WriteLine("Monaco Editor not ready yet, caching scroll request");
                _pendingScrollRequest = (lineNumber, column);
            }
        }

        public async void ClearEditorHighlight()
        {
            Console.WriteLine("ClearEditorHighlight called");
            Console.WriteLine($"_monacoEditor is null: {_monacoEditor == null}");
            Console.WriteLine($"_isMonacoReady: {_isMonacoReady}");
            
            if (_monacoEditor != null && _isMonacoReady)
            {
                await _monacoEditor.ClearHighlightAsync();
            }
        }

        private async void OpenPdfFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenPdfFileAsync();
        }

        public async Task OpenPdfFileAsync()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var dialog = new OpenFileDialog
            {
                Title = "打开 PDF 文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "PDF 文件", Extensions = { "pdf" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                await ShowPdfViewer(filePath);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private async Task ShowPdfViewer(string filePath)
        {
            var pdfTabItem = this.FindControl<TabItem>("PdfTabItem");
            var mainTabControl = this.FindControl<TabControl>("MainTabControl");
            var pdfFileName = this.FindControl<TextBlock>("PdfFileName");

            if (pdfTabItem != null && mainTabControl != null && pdfFileName != null)
            {
                _lastPdfFilePath = filePath;
                pdfFileName.Text = System.IO.Path.GetFileName(filePath);
                
                mainTabControl.SelectedItem = pdfTabItem;
                
                if (_pdfViewer != null)
                {
                    await _pdfViewer.LoadPdfAsync(filePath);
                }
            }
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
                    Console.WriteLine("Switching to Markdown Editor");
                    
                    // 先确保PDF完全隐藏
                    if (_pdfViewer != null)
                    {
                        _pdfViewer.Deactivate();
                    }
                    
                    // 等待一小段时间确保PDF WebView2完全隐藏
                    await Task.Delay(50);

                    if (_monacoEditor != null)
                    {
                        // 不强制重置，保留编辑内容
                        await _monacoEditor.Activate(false);
                    }

                    if (_previewWebView != null)
                    {
                        // 设置预览准备好后的回调，在内容更新完成后刷新高亮
                        _previewWebView.SetOnPreviewReadyCallback(async () =>
                        {
                            Console.WriteLine("MainWindow: Preview ready callback triggered");
                            if (_monacoEditor != null)
                            {
                                await _monacoEditor.RequestCurrentSelectionAsync();
                            }
                        });

                        // 激活预览器，它会自动更新内容
                        await _previewWebView.Activate(false);

                        // 强制刷新预览内容，确保data-pos属性正确加载
                        if (DataContext is MainWindowViewModel vm)
                        {
                            Console.WriteLine("MainTabControl_SelectionChanged: Forcing preview content refresh");
                            _previewWebView.SetContent(vm.PreviewHtml);
                        }
                    }
                }
            else if (selectedTab.Header?.ToString() == "PDF Reader")
            {
                Console.WriteLine("Switching to PDF Reader");
                
                if (_monacoEditor != null)
                {
                    _monacoEditor.Deactivate();
                }
                
                if (_previewWebView != null)
                {
                    _previewWebView.Deactivate();
                }
                
                if (!string.IsNullOrEmpty(_lastPdfFilePath) && _pdfViewer != null)
                {
                    await _pdfViewer.Activate();
                }
            }
        }
    }
}