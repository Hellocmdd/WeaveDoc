using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Runtime.InteropServices;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private WebView2 _webview;
        private CoreWebView2Controller _controller;
        private bool _isWebViewReady;
        private string _pendingContent = string.Empty;

        public MonacoEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            LayoutUpdated += OnLayoutUpdated;
            SizeChanged += OnSizeChanged;
            PropertyChanged += OnPropertyChanged;
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            InitializeWebViewAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            // 清理 WebView2 资源
            _controller?.Close();
            _webview?.Dispose();
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            Logger.Log("MonacoEditorControl: LayoutUpdated event triggered");
            UpdateControllerBounds();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            Logger.Log("MonacoEditorControl: SizeChanged event triggered");
            UpdateControllerBounds();
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
            {
                Logger.Log("MonacoEditorControl: Bounds property changed");
                UpdateControllerBounds();
            }
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                Logger.Log("MonacoEditorControl: Starting WebView2 initialization...");
                
                // 获取父窗口的 HWND
                var window = this.VisualRoot as Window;
                if (window == null)
                {
                    Logger.Log("MonacoEditorControl: VisualRoot is not a Window");
                    return;
                }

                var hwnd = window.PlatformImpl?.Handle?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log("MonacoEditorControl: Failed to get window HWND");
                    return;
                }

                Logger.Log($"MonacoEditorControl: Got HWND: {hwnd.ToInt32()}");

                // 使用应用级的 WebView2 环境
                if (App.WebView2Environment == null)
                {
                    Logger.Log("MonacoEditorControl: WebView2 environment is not initialized");
                    return;
                }

                // 创建 WebView2 控制器
                Logger.Log("MonacoEditorControl: Creating WebView2 controller...");
                _controller = await App.WebView2Environment.CreateCoreWebView2ControllerAsync(hwnd);
                _webview = _controller.CoreWebView2;

                if (_webview == null)
                {
                    Logger.Log("MonacoEditorControl: Failed to create WebView2 core");
                    return;
                }

                // 注册事件
                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                Logger.Log("MonacoEditorControl: WebView2 controller created");
                Logger.Log("MonacoEditorControl: WebView2 core initialized");

                // 初始更新控制器边界
                UpdateControllerBounds();

                // 加载 Monaco 编辑器
                var monacoPath = "file:///" + System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html").Replace('\\', '/');
                Logger.Log($"MonacoEditorControl: Loading Monaco from: {monacoPath}");
                _webview.Navigate(monacoPath);

                // 监听根窗口的布局变化
                if (window != null)
                {
                    window.LayoutUpdated += (s, e) =>
                    {
                        Logger.Log("MonacoEditorControl: Root window LayoutUpdated event triggered");
                        UpdateControllerBounds();
                    };
                }

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
                    // 使用更简单的方法计算位置和尺寸
                    // 直接使用控件的边界，因为 WebView2 是直接添加到窗口的
                    var bounds = this.Bounds;
                    
                    // Position WebView2 to cover this control's area
                    var x = (int)bounds.X + 8; // 加上边距
                    var y = (int)bounds.Y + 36; // 加上标题栏高度和边距
                    var w = Math.Max(0, (int)bounds.Width);
                    var h = Math.Max(0, (int)bounds.Height);
                    
                    _controller.Bounds = new System.Drawing.Rectangle(x, y, w, h);
                    Logger.Log($"MonacoEditorControl: Updated bounds: x={x}, y={y}, w={w}, h={h}");
                    
                    // 通知 Monaco 编辑器更新尺寸
                    Logger.Log("MonacoEditorControl: Calling UpdateMonacoSize");
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
                    // 通知 Monaco 编辑器更新尺寸
                    var script = $"window.resizeEditor({width}, {height});";
                    Logger.Log($"MonacoEditorControl: Executing script: {script}");
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
                else
                {
                    Logger.Log("MonacoEditorControl: _webview is null, cannot resize Monaco editor");
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
                    // 尝试反序列化 JSON
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
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                // 尝试获取 MainWindowViewModel
                                MainWindowViewModel? vm = null;
                                
                                // 首先检查当前控件的 DataContext
                                if (DataContext is MainWindowViewModel)
                                {
                                    vm = DataContext as MainWindowViewModel;
                                    Logger.Log("MonacoEditorControl: Found MainWindowViewModel in DataContext");
                                }
                                else
                                {
                                    // 如果当前控件的 DataContext 不是 MainWindowViewModel，尝试查找根窗口的 DataContext
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
                                else
                                {
                                    Logger.Log("MonacoEditorControl: Could not find MainWindowViewModel");
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
                    // 详细记录 JSON 解析错误和原始 JSON 数据
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

                    // 如果有待设置的内容，现在设置它
                    if (!string.IsNullOrEmpty(_pendingContent))
                    {
                        SetContentAsync(_pendingContent);
                        _pendingContent = string.Empty;
                    }
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

        public async Task SetContentAsync(string content)
        {
            try
            {
                if (!_isWebViewReady || _webview == null)
                {
                    // Store content to be set when WebView is ready
                    _pendingContent = content;
                    Logger.Log("MonacoEditorControl: WebView not ready, content queued");
                    return;
                }

                var obj = new { Type = "setContent", Data = content };
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                
                // PostWebMessageAsJson is not async, so we don't need await here.
                _webview.PostWebMessageAsJson(json);
                Logger.Log($"MonacoEditorControl: Content set (length: {content.Length})");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            
            // Add this to suppress CS1998 if no other await is present
            await Task.CompletedTask;
        }

        private class Message
        {
            public string? Type { get; set; }
            public object? Data { get; set; }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}