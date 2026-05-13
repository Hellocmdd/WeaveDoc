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
        private bool _isMonacoReady = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            _monacoEditor = this.FindControl<MonacoEditorControl>("MonacoEditor");
            _previewWebView = this.FindControl<PreviewWebViewControl>("PreviewWebView");

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
            if (_previewWebView != null)
            {
                _previewWebView.ScrollToSelection(startLine, startCol, endLine, endCol);
            }
        }

        public void SetMonacoReady(bool ready)
        {
            _isMonacoReady = ready;
            Console.WriteLine($"Monaco Editor ready: {ready}");
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
                Console.WriteLine("Monaco Editor not ready yet, ignoring scroll request");
            }
        }
    }
}
