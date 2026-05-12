using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class PreviewWebViewControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private bool _isInitialized = false;
        private string _pendingContent = string.Empty;
        private static CoreWebView2Environment? _sharedEnvironment;

        public PreviewWebViewControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, string>(
                nameof(HtmlContent),
                defaultBindingMode: BindingMode.OneWay);

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HtmlContentProperty)
            {
                var newContent = change.NewValue as string;
                UpdatePreview(newContent ?? string.Empty);
            }
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            _ = InitializeWebViewAsync();
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

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateControllerBounds();
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

                await Task.Delay(500);

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
                _webview.NavigationCompleted += Webview_NavigationCompleted;
                _webview.WebMessageReceived += Webview_WebMessageReceived;

                _webview.Settings.AreDefaultContextMenusEnabled = true;
                _webview.Settings.IsZoomControlEnabled = true;

                var jsScript = @"
        var scrollDebounceTimer = null;
        window.addEventListener('scroll', function(event) {
            try {
                if (scrollDebounceTimer) {
                    clearTimeout(scrollDebounceTimer);
                }
                scrollDebounceTimer = setTimeout(function() {
                    var doc = document.documentElement;
                    var body = document.body;
                    var scrollTop = window.scrollY || doc.scrollTop || body.scrollTop;
                    var scrollHeight = Math.max(doc.scrollHeight, body.scrollHeight);
                    var clientHeight = Math.max(doc.clientHeight, body.clientHeight);
                    var scrollableHeight = scrollHeight - clientHeight;
                    
                    if (scrollableHeight <= 0) {
                        scrollableHeight = 1;
                    }
                    
                    var scrollPercentage = (scrollTop / scrollableHeight) * 100;
                    scrollPercentage = Math.max(0, Math.min(100, scrollPercentage));
                    
                    var allElements = document.querySelectorAll('[data-line]');
                    var visibleLine = 1;
                    
                    var viewportMiddle = clientHeight / 2;
                    
                    for (var i = 0; i < allElements.length; i++) {
                        var el = allElements[i];
                        var rect = el.getBoundingClientRect();
                        
                        if (rect.top <= viewportMiddle && rect.bottom >= viewportMiddle) {
                            visibleLine = parseInt(el.getAttribute('data-line'), 10);
                            break;
                        }
                    }
                    
                    var scrollData = {
                        percentage: scrollPercentage,
                        visibleLine: visibleLine
                    };
                    
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage({ Type: 'previewScrollChanged', Data: JSON.stringify(scrollData) });
                    } else if (window.external && window.external.notify) {
                        window.external.notify(JSON.stringify({ Type: 'previewScrollChanged', Data: JSON.stringify(scrollData) }));
                    }
                }, 50);
            } catch (e) {
            }
        });
        
        window.updateContent = function(html) {
            var contentDiv = document.getElementById('content');
            if (contentDiv) {
                contentDiv.innerHTML = html;
            }
        };
        
        window.scrollToPercentage = function(percentage, firstLine, totalLines) {
            try {
                var doc = document.documentElement;
                var body = document.body;
                var scrollHeight = Math.max(doc.scrollHeight, body.scrollHeight);
                var clientHeight = Math.max(doc.clientHeight, body.clientHeight);
                var scrollableHeight = scrollHeight - clientHeight;
                
                if (scrollableHeight <= 0) {
                    return;
                }
                
                var scrollTop = (scrollableHeight * percentage) / 100;
                window.scrollTo(0, scrollTop);
            } catch (e) {
            }
        };
    ";

                var html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {
            box-sizing: border-box;
        }
        html, body {
            height: 100%;
            margin: 0;
            padding: 0;
            overflow: auto;
        }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif;
            font-size: 14px;
            line-height: 1.4;
            color: #333;
            background-color: #ffffff;
            padding: 12px;
            overflow-wrap: break-word;
        }
        h1, h2, h3, h4, h5, h6 {
            font-weight: 600;
            margin-top: 12px;
            margin-bottom: 8px;
            line-height: 1.3;
        }
        h1 { font-size: 1.5em; border-bottom: 1px solid #eee; padding-bottom: 0.2em; }
        h2 { font-size: 1.25em; border-bottom: 1px solid #eee; padding-bottom: 0.2em; }
        h3 { font-size: 1.1em; }
        h4 { font-size: 1em; }
        p { margin-top: 0; margin-bottom: 8px; }
        ul, ol { padding-left: 1.5em; margin-top: 0; margin-bottom: 8px; }
        li { margin-top: 2px; }
        code {
            font-family: 'Fira Code', 'Monaco', 'Consolas', monospace;
            font-size: 85%;
            padding: 0.15em 0.3em;
            background-color: rgba(27, 31, 35, 0.05);
            border-radius: 2px;
        }
        pre {
            padding: 12px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.4;
            background-color: #f6f8fa;
            border-radius: 3px;
            margin-bottom: 8px;
        }
        pre code {
            padding: 0;
            background-color: transparent;
            border-radius: 0;
        }
        blockquote {
            margin: 4px 0 8px 0;
            padding: 4px 12px;
            color: #6a737d;
            border-left: 3px solid #dfe2e5;
            background-color: #f8f9fa;
        }
        a { color: #0366d6; text-decoration: none; }
        a:hover { text-decoration: underline; }
        hr {
            height: 1px;
            padding: 0;
            margin: 12px 0;
            background-color: #e1e4e8;
            border: 0;
        }
        table {
            border-spacing: 0;
            border-collapse: collapse;
            margin-bottom: 8px;
            width: 100%;
            font-size: 13px;
        }
        table th, table td {
            padding: 4px 8px;
            border: 1px solid #dfe2e5;
        }
        table th { font-weight: 600; background-color: #f6f8fa; }
        table tr:nth-child(2n) { background-color: #f6f8fa; }
        img { max-width: 100%; box-sizing: content-box; }
        #content { min-height: 100%; }
    </style>
</head>
<body>
    <div id='content'>Welcome to WeaveDoc Preview</div>
    <script>
        window.updateContent = function(html) {
            var contentDiv = document.getElementById('content');
            if (contentDiv) {
                contentDiv.innerHTML = html;
            }
        };
        " + jsScript + @"
</script>
</body>
</html>";
                _webview.NavigateToString(html);
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
                _isInitialized = true;

                UpdateControllerBounds();

                if (!string.IsNullOrEmpty(_pendingContent))
                {
                    UpdatePreview(_pendingContent);
                    _pendingContent = string.Empty;
                }
            }
        }

        private async void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                if (string.IsNullOrWhiteSpace(json)) return;

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

                if (msgType == "previewScrollChanged" && msgData != null)
                {
                    try
                    {
                        var scrollData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = scrollData.RootElement;

                        double scrollPercentage = 0;
                        int visibleLine = 1;

                        if (root.TryGetProperty("percentage", out var pctProp))
                        {
                            scrollPercentage = pctProp.GetDouble();
                        }
                        if (root.TryGetProperty("visibleLine", out var lineProp))
                        {
                            visibleLine = lineProp.GetInt32();
                        }

                        var rootWindow = this.VisualRoot as Window;
                        if (rootWindow is MainWindow mainWindow)
                        {
                            await mainWindow.SyncEditorScrollAsync(scrollPercentage, visibleLine);
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

        private async void UpdatePreview(string content)
        {
            try
            {
                if (_webview == null)
                {
                    _pendingContent = content;
                    return;
                }

                if (!_isInitialized)
                {
                    _pendingContent = content;
                    return;
                }

                var script = $"window.updateContent({System.Text.Json.JsonSerializer.Serialize(content)});";

                await _webview.ExecuteScriptAsync(script);
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
                if (root == null)
                {
                    Logger.Log("PreviewWebViewControl: No root window found for bounds update");
                    return;
                }

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
            }
            catch (Exception ex)
            {
            }
        }

        public void SetContent(string content)
        {
            HtmlContent = content;
        }

        public async void ScrollToPercentage(double percentage, int firstLine, int totalLines)
        {
            try
            {
                if (_webview == null || !_isInitialized)
                {
                    return;
                }

                percentage = Math.Max(0, Math.Min(100, percentage));

                var script = $@"
                (function() {{
                    var doc = document.documentElement;
                    var body = document.body;
                    var scrollHeight = Math.max(doc.scrollHeight, body.scrollHeight);
                    var clientHeight = Math.max(doc.clientHeight, body.clientHeight);
                    var scrollableHeight = scrollHeight - clientHeight;
                    if (scrollableHeight <= 0) return;
                    var scrollTop = (scrollableHeight * {percentage}) / 100;
                    window.scrollTo(0, scrollTop);
                }})();
                ";

                await _webview.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
            }
        }

        public async void ScrollToLine(int lineNumber)
        {
            try
            {
                if (_webview == null || !_isInitialized)
                {
                    return;
                }

                var script = $@"
                    (function() {{
                        var targetLine = {lineNumber};
                        var targetElement = null;
                        var allElements = document.querySelectorAll('[data-line]');
                        
                        for (var i = 0; i < allElements.length; i++) {{
                            var el = allElements[i];
                            var elLine = parseInt(el.getAttribute('data-line'), 10);
                            
                            if (elLine === targetLine) {{
                                targetElement = el;
                                break;
                            }}
                            if (elLine > targetLine) {{
                                if (i > 0) {{
                                    targetElement = allElements[i - 1];
                                }} else {{
                                    targetElement = el;
                                }}
                                break;
                            }}
                        }}
                        
                        if (!targetElement && allElements.length > 0) {{
                            targetElement = allElements[allElements.length - 1];
                        }}
                        
                        if (targetElement) {{
                            targetElement.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                        }}
                    }})();
                ";

                await _webview.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
