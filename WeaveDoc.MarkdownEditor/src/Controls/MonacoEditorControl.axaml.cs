using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private CoreWebView2Controller? _controller;
        private CoreWebView2? _webview;
        private bool _initialized;
        private string _pendingContent = string.Empty;
        private bool _isWebViewReady;

        public MonacoEditorControl()
        {
            InitializeComponent();
            this.AttachedToVisualTree += (_, _) => _ = InitializeWebViewAsync();
            this.DetachedFromVisualTree += (_, _) => Cleanup();
        }

        private async Task InitializeWebViewAsync()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Logger.Log("MonacoEditorControl: Starting WebView2 initialization...");

                // 等待控件完全加载并附加到视觉树
                await Task.Delay(100); 

                // Get top-level window handle
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    Logger.Log("MonacoEditorControl: root window is null");
                    return;
                }

                // 【修复】正确获取 Windows 窗口句柄 (HWND) - Avalonia 11+ 方式
                var topLevel = root.TryGetPlatformHandle();
                
                if (topLevel == null || topLevel.Handle == IntPtr.Zero)
                {
                    Logger.Log("MonacoEditorControl: Could not get platform handle or HWND is zero");
                    return;
                }

                var hwnd = topLevel.Handle;
                Logger.Log($"MonacoEditorControl: Got HWND: {hwnd}");

                // 检查 WebView2 环境是否已经创建
                if (App.WebView2Environment == null)
                {
                    Logger.Log("MonacoEditorControl: WebView2 environment is not yet created, waiting...");
                    // 等待 WebView2 环境创建
                    await Task.Delay(1000);
                    if (App.WebView2Environment == null)
                    {
                        Logger.Log("MonacoEditorControl: WebView2 environment is still null after waiting");
                        return;
                    }
                }

                // 【修复】在 UI 线程上创建 WebView2 环境，避免线程模式冲突
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        // 使用应用程序级别的 WebView2 环境，避免线程模式冲突
                        var env = App.WebView2Environment;
                        if (env == null)
                        {
                            Logger.Log("MonacoEditorControl: WebView2 environment is null");
                            return;
                        }
                        
                        Logger.Log("MonacoEditorControl: Creating WebView2 controller...");
                        try
                        {
                            // 【修复】使用 CreateCoreWebView2ControllerAsync 的重载，传入 IntPtr (HWND)
                            _controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
                            Logger.Log("MonacoEditorControl: WebView2 controller created");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex);
                            return;
                        }
                        
                        _webview = _controller.CoreWebView2;
                        Logger.Log("MonacoEditorControl: WebView2 core initialized");

                        // Configure WebView2 settings
                        _webview.Settings.IsStatusBarEnabled = false;
                        _webview.Settings.AreDefaultContextMenusEnabled = true;
                        _webview.Settings.AreDevToolsEnabled = true;

                        // Set initial bounds
                        UpdateControllerBounds(root);

                        // Monitor layout changes to resize controller
                        this.LayoutUpdated += (_, __) => UpdateControllerBounds(root);
                        root.SizeChanged += (_, __) => UpdateControllerBounds(root);

                        // Wire up messages from web
                        _webview.WebMessageReceived += Webview_WebMessageReceived;

                        // Mark WebView as ready when navigation completes
                        _webview.NavigationCompleted += (s, e) =>
                        {
                            _isWebViewReady = true;
                            Logger.Log("MonacoEditorControl: WebView navigation completed");
                            
                            // 【新增】强制打开开发者工具以便调试
                            try 
                            {
                                _webview.OpenDevToolsWindow();
                            } 
                            catch (Exception devEx) 
                            {
                                Logger.LogException(devEx);
                            }

                            // If there's pending content, set it now
                            if (!string.IsNullOrEmpty(_pendingContent))
                            {
                                _ = SetContentAsync(_pendingContent);
                                _pendingContent = string.Empty;
                            }
                        };

                        // Load the local index.html from output directory
                        var exeDir = AppContext.BaseDirectory;
                        var indexPath = Path.Combine(exeDir, "Assets", "monaco-editor", "index.html");
                        
                        if (!File.Exists(indexPath))
                        {
                            // Try fallback to project dir
                            var projectDir = Directory.GetCurrentDirectory();
                            indexPath = Path.Combine(projectDir, "Assets", "monaco-editor", "index.html");
                            Logger.Log($"MonacoEditorControl: Trying fallback path: {indexPath}");
                        }

                        if (!File.Exists(indexPath))
                        {
                            Logger.Log($"MonacoEditorControl: index.html not found at {indexPath}");
                            return;
                        }

                        var uri = new Uri(indexPath).AbsoluteUri;
                        Logger.Log($"MonacoEditorControl: Loading Monaco from: {uri}");
                        _webview.Navigate(uri);

                        // If DataContext is a MainWindowViewModel, set initial content
                        if (DataContext is MainWindowViewModel mwvm)
                        {
                            var initialContent = mwvm.EditorContent ?? string.Empty;
                            if (!string.IsNullOrEmpty(initialContent))
                            {
                                await SetContentAsync(initialContent);
                            }
                        }

                        Logger.Log("MonacoEditorControl: WebView2 initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void Cleanup()
        {
            try
            {
                if (_webview != null)
                {
                    _webview.WebMessageReceived -= Webview_WebMessageReceived;
                    // 注意：C# 中移除事件通常不需要指定右侧为 null，直接 -= 方法名即可，但保留原逻辑结构
                    // _webview.NavigationCompleted -= null; 这行在原代码中可能无效或报错，建议注释掉或移除
                    _webview = null;
                }
                _controller?.Close();
                _controller = null;
                _isWebViewReady = false;
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
                    // Calculate the bounds of this control relative to the window
                    var controlBounds = this.Bounds;
                    // var windowBounds = root.Bounds; // 未使用，可保留或删除
                    
                    // Position WebView2 to cover this control's area
                    var x = (int)controlBounds.X;
                    var y = (int)controlBounds.Y;
                    var w = Math.Max(0, (int)controlBounds.Width);
                    var h = Math.Max(0, (int)controlBounds.Height);
                    
                    _controller.Bounds = new System.Drawing.Rectangle(x, y, w, h);
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
                
                var msg = System.Text.Json.JsonSerializer.Deserialize<Message?>(json);
                if (msg == null) return;

                if (msg.Type == "contentChanged")
                {
                    var content = msg.Data?.ToString() ?? string.Empty;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            if (DataContext is MainWindowViewModel vm)
                            {
                                vm.EditorContent = content;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex);
                        }
                    });
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

        private record Message(string? Type, object? Data);

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}