using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private string _pendingContent = string.Empty;
        private bool _isInitializing = false;
        private bool _isBoundsSet = false;
        private static CoreWebView2Environment? _sharedEnvironment;

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
            if (!_isInitializing)
            {
                _isInitializing = true;
                await InitializeWebViewAsync();
            }
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            if (_controller != null)
            {
                _controller.Close();
                _controller = null;
            }
            if (_webview != null)
            {
                _webview.WebMessageReceived -= Webview_WebMessageReceived;
                _webview.NavigationCompleted -= Webview_NavigationCompleted;
                _webview = null;
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    return;
                }

                await Task.Delay(300);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                if (_sharedEnvironment == null)
                {
                    _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions 
                    { 
                        AdditionalBrowserArguments = "--disable-gpu --disable-software-rasterizer --disable-dev-shm-usage --no-sandbox",
                        AllowSingleSignOnUsingOSPrimaryAccount = false
                    });
                }

                _controller = await _sharedEnvironment.CreateCoreWebView2ControllerAsync(hwnd);

                _controller.IsVisible = true;
                _controller.CoreWebView2.Settings.IsScriptEnabled = true;

                _webview = _controller.CoreWebView2;
                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                _webview.Settings.AreDefaultContextMenusEnabled = false;

                var htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html");

                _webview.Navigate(htmlPath);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdateControllerBounds(bool force = false)
        {
            try
            {
                if (_controller == null) return;

                var root = this.VisualRoot as Window;
                if (root == null) return;

                var scaling = root.RenderScaling;

                var transform = this.TransformToVisual(root);
                var position = transform?.Transform(new Point(0, 0)) ?? new Point(0, 0);

                var width = (int)(this.Bounds.Width * scaling);
                var height = (int)(this.Bounds.Height * scaling);
                var x = (int)(position.X * scaling);
                var y = (int)(position.Y * scaling);

                width = Math.Max(100, width);
                height = Math.Max(100, height);

                _controller.Bounds = new System.Drawing.Rectangle(x, y, width, height);
                _isBoundsSet = true;
            }
            catch (Exception ex)
            {
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateControllerBounds();

            if (_webview != null)
            {
                _ = ResizeMonacoEditorAsync((int)(e.NewSize.Width * 2), (int)(e.NewSize.Height * 2));
            }
        }

        private async Task ResizeMonacoEditorAsync(int width, int height)
        {
            try
            {
                if (_webview == null) return;

                var script = $"window.resizeEditor({width}, {height});";
                await _webview.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
            }
        }

        private void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                
                if (string.IsNullOrWhiteSpace(json)) 
                {
                    return;
                }

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var jsonRoot = doc.RootElement;

                string? msgType = null;
                string? msgData = null;

                if (jsonRoot.TryGetProperty("Type", out var typeProp))
                {
                    msgType = typeProp.GetString();
                }

                if (jsonRoot.TryGetProperty("Data", out var dataProp))
                {
                    msgData = dataProp.GetString();
                }

                if (msgType == "contentChanged" && msgData != null)
                {
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
                else if (msgType == "scrollChanged" && msgData != null)
                {
                    try
                    {
                        var scrollData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = scrollData.RootElement;

                        double scrollPercentage = 0;
                        int firstLine = 1;
                        int totalLines = 1;

                        if (root.TryGetProperty("percentage", out var pctProp))
                        {
                            scrollPercentage = pctProp.GetDouble();
                        }
                        if (root.TryGetProperty("firstLine", out var firstLineProp))
                        {
                            firstLine = firstLineProp.GetInt32();
                        }
                        if (root.TryGetProperty("totalLines", out var totalLinesProp))
                        {
                            totalLines = totalLinesProp.GetInt32();
                        }

                        var rootWindow = this.VisualRoot as Window;
                        if (rootWindow is MainWindow mainWindow)
                        {
                            mainWindow.SyncPreviewScroll(scrollPercentage, firstLine, totalLines);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void Webview_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                await Task.Delay(200);

                UpdateControllerBounds(true);

                await Task.Delay(100);

                if (_webview != null && _controller != null)
                {
                    var root = VisualRoot as Window;
                    if (root != null)
                    {
                        var scaling = root.RenderScaling;
                        var width = (int)(this.Bounds.Width * scaling * 2);
                        var height = (int)(this.Bounds.Height * scaling * 2);

                        await ResizeMonacoEditorAsync(width, height);
                    }
                }

                if (!string.IsNullOrEmpty(_pendingContent))
                {
                    SetContentAsync(_pendingContent);
                    _pendingContent = string.Empty;
                }
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
            }
        }

        public async Task ScrollToLineAsync(int lineNumber)
        {
            try
            {
                if (_webview == null)
                {
                    return;
                }

                var script = $"window.scrollToLine({lineNumber});";
                await _webview.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
            }
        }

        public async Task ScrollToPercentageAsync(double percentage)
        {
            try
            {
                if (_webview == null)
                {
                    return;
                }

                percentage = Math.Max(0, Math.Min(100, percentage));

                var script = $@"
                    (function() {{
                        if (!editor) return;
                        var scrollHeight = editor.getScrollHeight();
                        var layoutInfo = editor.getLayoutInfo();
                        var scrollableHeight = scrollHeight - layoutInfo.height;
                        if (scrollableHeight <= 0) return;
                        var scrollTop = (scrollableHeight * {percentage}) / 100;
                        editor.setScrollTop(scrollTop);
                    }})();
                ";
                await _webview.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
