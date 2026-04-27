using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using System;
using System.Runtime.InteropServices;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        private CoreWebView2? _previewWebView;
        private CoreWebView2Controller? _previewController;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public MainWindow()
        {
            InitializeComponent();
            // 将 MainWindowViewModel 设置为 DataContext
            var vm = new MainWindowViewModel();
            DataContext = vm;
            Logger.Log("MainWindow: Constructor called");
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            Logger.Log("MainWindow: Events subscribed");
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            Logger.Log("MainWindow: OnLoaded called");
            // 延迟一下，确保控件完全加载
            await Task.Delay(100);
            Logger.Log("MainWindow: Calling InitializePreviewWebViewAsync");
            InitializePreviewWebViewAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            _previewController?.Close();
        }

        private void OnSizeChanged(object? sender, EventArgs e)
        {
            UpdatePreviewControllerBounds();
        }

        private async void InitializePreviewWebViewAsync()
        {
            try
            {
                Logger.Log("MainWindow: Starting WebView2 initialization for preview...");

                // 使用 P/Invoke 获取窗口句柄
                var hwnd = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log("MainWindow: Failed to get window handle");
                    return;
                }

                Logger.Log($"MainWindow: Got window handle: {hwnd}");

                var env = await CoreWebView2Environment.CreateAsync();
                Logger.Log("MainWindow: Created WebView2 environment for preview");

                _previewController = await env.CreateCoreWebView2ControllerAsync(hwnd);
                Logger.Log("MainWindow: Created WebView2 controller for preview");

                _previewWebView = _previewController.CoreWebView2;

                // 导航到一个空白页面
                _previewWebView.NavigateToString("<html><body></body></html>");

                // 初始更新预览内容
                UpdatePreviewContent();

                // 初始更新预览区域大小和位置
                UpdatePreviewControllerBounds();

                // 监听 EditorContent 变化，更新预览
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.EditorContent))
                        {
                            UpdatePreviewContent();
                        }
                    };
                }

                Logger.Log("MainWindow: WebView2 initialized successfully for preview");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdatePreviewContent()
        {
            try
            {
                if (_previewWebView != null && DataContext is MainWindowViewModel vm)
                {
                    var previewHtml = vm.PreviewHtml;
                    _previewWebView.Navigate(previewHtml);
                    Logger.Log("MainWindow: Updated preview content");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdatePreviewControllerBounds()
        {
            try
            {
                Logger.Log("MainWindow: UpdatePreviewControllerBounds called");
                if (_previewController == null)
                {
                    Logger.Log("MainWindow: _previewController is null");
                    return;
                }

                // 计算预览区域在窗口中的位置和大小
                var previewScrollViewer = this.FindControl<ScrollViewer>("PreviewScrollViewer");
                if (previewScrollViewer != null)
                {
                    var bounds = previewScrollViewer.Bounds;
                    Logger.Log($"MainWindow: PreviewScrollViewer bounds: {bounds}");
                    var point = previewScrollViewer.TranslatePoint(new Avalonia.Point(0, 0), this);
                    if (point != null)
                    {
                        var x = (int)point.Value.X;
                        var y = (int)point.Value.Y;
                        var w = Math.Max(0, (int)bounds.Width);
                        var h = Math.Max(0, (int)bounds.Height);

                        _previewController.Bounds = new System.Drawing.Rectangle(x, y, w, h);
                        Logger.Log($"MainWindow: Updated preview bounds: x={x}, y={y}, w={w}, h={h}");
                    }
                    else
                    {
                        Logger.Log("MainWindow: TranslatePoint returned null");
                    }
                }
                else
                {
                    Logger.Log("MainWindow: PreviewScrollViewer not found");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        
        /// <summary>
        /// 打开 Markdown 文件
        /// </summary>
        /// <returns></returns>
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
        
        /// <summary>
        /// 处理打开文件菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OpenFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenMarkdownFileAsync();
        }
        
        /// <summary>
        /// 保存 Markdown 文件
        /// </summary>
        /// <returns></returns>
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
        
        /// <summary>
        /// 另存为 Markdown 文件
        /// </summary>
        /// <returns></returns>
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
        
        /// <summary>
        /// 处理保存文件菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void SaveFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsync();
        }
        
        /// <summary>
        /// 处理另存为菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void SaveAsFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsAsync();
        }
        
        // 工具栏按钮事件处理程序
        
        /// <summary>
        /// 处理粗体按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BoldButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("**", "**");
        }
        
        /// <summary>
        /// 处理斜体按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ItalicButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("*", "*");
        }
        
        /// <summary>
        /// 处理下划线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void UnderlineButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("<u>", "</u>");
        }
        
        /// <summary>
        /// 处理一级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H1Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("# ", "");
        }
        
        /// <summary>
        /// 处理二级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H2Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("## ", "");
        }
        
        /// <summary>
        /// 处理三级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H3Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("### ", "");
        }
        
        /// <summary>
        /// 处理无序列表按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BulletListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("- ", "");
        }
        
        /// <summary>
        /// 处理有序列表按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void NumberedListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("1. ", "");
        }
        
        /// <summary>
        /// 处理代码块按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void CodeBlockButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("```\n", "\n```");
        }
        
        /// <summary>
        /// 处理链接按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void LinkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("[链接文本](", ")");
        }
        
        /// <summary>
        /// 处理图片按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ImageButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("![图片描述](", ")");
        }
        
        /// <summary>
        /// 在编辑器中插入 Markdown 语法
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <param name="suffix">后缀</param>
        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // 简单实现：在编辑器内容末尾添加语法
                vm.EditorContent += prefix + suffix;
            }
        }
    }
}