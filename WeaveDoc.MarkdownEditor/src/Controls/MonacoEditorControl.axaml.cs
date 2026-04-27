using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Services;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private bool _isWebViewReady;
        private string _pendingContent = string.Empty;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public MonacoEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            LayoutUpdated += OnLayoutUpdated;
            SizeChanged += OnSizeChanged;
            PropertyChanged += OnPropertyChanged;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            // 延迟一下，确保控件完全加载
            await Task.Delay(100);
            InitializeWebViewAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            _controller?.Close();
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            UpdateControllerBounds();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateControllerBounds();
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
            {
                UpdateControllerBounds();
            }
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                Logger.Log("MonacoEditorControl: Starting WebView2 initialization...");

                // 获取包含 MonacoEditorControl 的窗口句柄
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    Logger.Log("MonacoEditorControl: Failed to get root window");
                    return;
                }

                // 等待窗口完全加载
                await Task.Delay(500);

                // 使用 P/Invoke 获取窗口句柄
                var hwnd = GetActiveWindow();
                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log("MonacoEditorControl: Failed to get window handle");
                    return;
                }

                Logger.Log($"MonacoEditorControl: Got window handle: {hwnd}");

                var env = await CoreWebView2Environment.CreateAsync();
                Logger.Log("MonacoEditorControl: Created WebView2 environment");

                _controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
                Logger.Log("MonacoEditorControl: Created WebView2 controller");

                _webview = _controller.CoreWebView2;

                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                var htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html");
                Logger.Log($"MonacoEditorControl: Loading Monaco Editor from: {htmlPath}");
                _webview.Navigate(htmlPath);

                Logger.Log("MonacoEditorControl: WebView2 initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdateControllerBounds(Window? root = null)
        {
            try
            {
                if (_controller == null) return;

                if (root == null)
                    root = this.VisualRoot as Window;

                if (root != null)
                {
                    // 计算控件在窗口中的位置和大小
                    var bounds = this.Bounds;
                    
                    // 计算控件在窗口中的绝对位置
                    var point = this.PointToScreen(new Point(0, 0));
                    var windowPoint = root.PointToScreen(new Point(0, 0));
                    var x = (int)(point.X - windowPoint.X);
                    var y = (int)(point.Y - windowPoint.Y);
                    var w = Math.Max(0, (int)bounds.Width);
                    var h = Math.Max(0, (int)bounds.Height);
                    
                    _controller.Bounds = new System.Drawing.Rectangle(x, y, w, h);
                    Logger.Log($"MonacoEditorControl: Updated bounds: x={x}, y={y}, w={w}, h={h}");
                    
                    UpdateMonacoSize(w, h);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdateMonacoSize(int width, int height)
        {
            try
            {
                if (_webview != null)
                {
                    var script = $"window.resizeEditor({width}, {height});";
                    _webview.ExecuteScriptAsync(script).ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            Logger.Log($"MonacoEditorControl: Resized Monaco editor to {width}x{height}");
                        }
                        else if (task.IsFaulted)
                        {
                            Logger.LogException(task.Exception);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                if (string.IsNullOrWhiteSpace(json)) return;
                
                Logger.Log($"MonacoEditorControl: Received message: {json}");
                
                try
                {
                    var msg = System.Text.Json.JsonSerializer.Deserialize<Message?>(json);
                    if (msg == null)
                    {
                        Logger.Log("MonacoEditorControl: Deserialized message is null");
                        return;
                    }

                    Logger.Log($"MonacoEditorControl: Deserialized message - Type: {msg.Type}, Data: {msg.Data}");

                    if (msg.Type == "contentChanged")
                    {
                        var content = msg.Data?.ToString() ?? string.Empty;
                        Logger.Log($"MonacoEditorControl: Content changed: {content}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                MainWindowViewModel? vm = null;
                                
                                if (DataContext is MainWindowViewModel)
                                {
                                    vm = DataContext as MainWindowViewModel;
                                    Logger.Log("MonacoEditorControl: Found MainWindowViewModel in DataContext");
                                }
                                else
                                {
                                    var root = this.VisualRoot;
                                    if (root is Window window && window.DataContext is MainWindowViewModel)
                                    {
                                        vm = window.DataContext as MainWindowViewModel;
                                        Logger.Log("MonacoEditorControl: Found MainWindowViewModel in root window DataContext");
                                    }
                                    else
                                    {
                                        Logger.Log("MonacoEditorControl: Could not find MainWindowViewModel in DataContext or root window");
                                    }
                                }
                                
                                if (vm != null)
                                {
                                    Logger.Log($"MonacoEditorControl: Updating EditorContent: {content}");
                                    vm.EditorContent = content;
                                    Logger.Log($"MonacoEditorControl: EditorContent updated successfully");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogException(ex);
                            }
                        });
                    }
                }
                catch (Exception jsonEx)
                {
                    Logger.Log($"MonacoEditorControl: JSON parsing error: {jsonEx.Message}");
                    Logger.Log($"MonacoEditorControl: Raw JSON: {json}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void Webview_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            try
            {
                if (args.IsSuccess)
                {
                    Logger.Log("MonacoEditorControl: WebView navigation completed");
                    _isWebViewReady = true;

                    // 使用 Dispatcher.UIThread.Post 延迟执行隐藏加载文本的操作，确保在控件完全加载后执行
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            // 隐藏加载文本
                            if (HostBorder != null)
                            {
                                HostBorder.Child = null;
                                Logger.Log("MonacoEditorControl: Hidden loading text");
                            }
                            else
                            {
                                Logger.Log("MonacoEditorControl: HostBorder is null, cannot hide loading text");
                            }

                            // 导航完成后手动更新控制器大小和位置
                            UpdateControllerBounds();
                            Logger.Log("MonacoEditorControl: Updated bounds after navigation");

                            if (!string.IsNullOrEmpty(_pendingContent))
                            {
                                SetContentAsync(_pendingContent);
                                _pendingContent = string.Empty;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex);
                        }
                    });
                }
                else
                {
                    Logger.Log($"MonacoEditorControl: WebView navigation failed with error: {args.WebErrorStatus}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void SetContentAsync(string content)
        {
            try
            {
                if (!_isWebViewReady || _webview == null)
                {
                    _pendingContent = content;
                    Logger.Log("MonacoEditorControl: WebView not ready, content queued");
                    return;
                }

                var obj = new { Type = "setContent", Data = content };
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                
                _webview.PostWebMessageAsJson(json);
                Logger.Log($"MonacoEditorControl: Content set (length: {content.Length})");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private class Message
        {
            public string? Type { get; set; }
            public object? Data { get; set; }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}