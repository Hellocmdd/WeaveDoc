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

                // Get top-level window handle
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    Logger.Log("MonacoEditorControl: root window is null");
                    return;
                }

                var platformImpl = root.PlatformImpl;
                if (platformImpl == null)
                {
                    Logger.Log("MonacoEditorControl: platform implementation is null");
                    return;
                }

                var platformHandle = ((IPlatformHandle)platformImpl).Handle;

                // Create WebView2 environment and controller
                var env = await CoreWebView2Environment.CreateAsync();
                _controller = await env.CreateCoreWebView2ControllerAsync(platformHandle);
                _webview = _controller.CoreWebView2;

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
        }

        private void Cleanup()
        {
            try
            {
                if (_webview != null)
                {
                    _webview.WebMessageReceived -= Webview_WebMessageReceived;
                    _webview.NavigationCompleted -= null;
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
                    var windowBounds = root.Bounds;
                    
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

// ... existing code ...

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
                // However, to keep the method signature async Task and suppress the warning,
                // we can use Task.CompletedTask or ensure there's an actual async call.
                // Since PostWebMessageAsJson is synchronous, we can just call it.
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

// ... existing code ...

        private record Message(string? Type, object? Data);

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}