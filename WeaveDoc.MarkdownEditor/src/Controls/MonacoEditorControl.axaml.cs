using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private string _pendingContent = string.Empty;
        private bool _isInitializing = false;

        public MonacoEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private async void OnLoaded(object? sender, EventArgs e)
        {
            Logger.Log("MonacoEditorControl: OnLoaded called");
            if (!_isInitializing)
            {
                _isInitializing = true;
                await InitializeWebViewAsync();
            }
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            Logger.Log("MonacoEditorControl: OnUnloaded called");
            _controller?.Close();
            _controller = null;
            _webview = null;
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                Logger.Log("MonacoEditorControl: Starting WebView2 initialization...");

                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    Logger.Log("MonacoEditorControl: Failed to get root window");
                    return;
                }

                await Task.Delay(300);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
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

                UpdateControllerBounds();
                _controller.IsVisible = true;

                _webview = _controller.CoreWebView2;
                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                _webview.Settings.AreDefaultContextMenusEnabled = false;

                var htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html");
                Logger.Log($"MonacoEditorControl: Loading Monaco Editor from: {htmlPath}");

                _webview.Navigate(htmlPath);

                Logger.Log("MonacoEditorControl: WebView2 initialization completed");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdateControllerBounds()
        {
            try
            {
                if (_controller == null) return;

                var root = this.VisualRoot as Window;
                if (root == null) return;

                var scaling = root.RenderScaling;
                var transform = this.TransformToVisual(null);
                var position = transform?.Transform(new Point(0, 0)) ?? new Point(0, 0);

                var width = (int)(this.Bounds.Width * scaling);
                var height = (int)(this.Bounds.Height * scaling);
                var x = (int)(position.X * scaling);
                var y = (int)(position.Y * scaling);

                width = Math.Max(100, width);
                height = Math.Max(100, height);

                _controller.Bounds = new System.Drawing.Rectangle(x, y, width, height);
                Logger.Log($"MonacoEditorControl: Bounds updated - X:{x}, Y:{y}, Width:{width}, Height:{height}, Scaling:{scaling}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateControllerBounds();
            
            if (_webview != null)
            {
                ResizeMonacoEditorAsync((int)(e.NewSize.Width * 2), (int)(e.NewSize.Height * 2));
            }
        }

        private async void ResizeMonacoEditorAsync(int width, int height)
        {
            try
            {
                if (_webview == null) return;

                var script = $"window.resizeEditor({width}, {height});";
                await _webview.ExecuteScriptAsync(script);
                Logger.Log($"MonacoEditorControl: Resized Monaco editor to {width}x{height}");
            }
            catch (Exception ex)
            {
                Logger.Log($"MonacoEditorControl: Failed to resize Monaco editor: {ex.Message}");
            }
        }

        private void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                if (string.IsNullOrWhiteSpace(json)) return;

                Logger.Log($"MonacoEditorControl: Received message: {json}");

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? msgType = null;
                string? msgData = null;

                if (root.TryGetProperty("Type", out var typeProp))
                {
                    msgType = typeProp.GetString();
                }

                if (root.TryGetProperty("Data", out var dataProp))
                {
                    msgData = dataProp.GetString();
                }

                if (msgType == "contentChanged" && msgData != null)
                {
                    Logger.Log($"MonacoEditorControl: Content changed: {msgData.Length} chars");
                    
                    MainWindowViewModel? vm = null;

                    if (DataContext is MainWindowViewModel)
                    {
                        vm = DataContext as MainWindowViewModel;
                    }
                    else
                    {
                        var visualRoot = this.VisualRoot;
                        if (visualRoot is Window window && window.DataContext is MainWindowViewModel)
                        {
                            vm = window.DataContext as MainWindowViewModel;
                        }
                    }

                    if (vm != null)
                    {
                        vm.EditorContent = msgData;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void Webview_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                Logger.Log("MonacoEditorControl: WebView navigation completed");

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

        public void SetContentAsync(string content)
        {
            try
            {
                if (_webview == null)
                {
                    _pendingContent = content;
                    return;
                }

                var obj = new { Type = "setContent", Data = content };
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                _webview.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }
    }
}
