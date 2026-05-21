using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public class WeaveDocHost
    {
        private readonly PreviewWebViewControl _control;

        public WeaveDocHost(PreviewWebViewControl control)
        {
            _control = control;
        }

        public void OnPreviewClick(int line, int column)
        {
            Console.WriteLine($"WeaveDocHost.OnPreviewClick: line={line}, column={column}");
            _control.HandlePreviewClick(line, column);
        }

        public void OnDebug(string message)
        {
            Console.WriteLine($"WeaveDocHost Debug: {message}");
        }
    }

    public partial class PreviewWebViewControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private bool _isInitialized = false;
        private bool _isActive = true;
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
            // 只隐藏，不关闭，以保留内容
            if (_controller != null)
            {
                _controller.IsVisible = false;
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            _ = HandleSizeChangeAsync();
        }

        private async Task HandleSizeChangeAsync()
        {
            try
            {
                await Task.Delay(100);
                
                UpdateControllerBounds();
                
                if (_webview != null && _isInitialized)
                {
                    await _webview.ExecuteScriptAsync("window.dispatchEvent(new Event('resize'));");
                }
            }
            catch
            {
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    Console.WriteLine("InitializeWebViewAsync: No root window");
                    return;
                }

                // 减少延迟时间
                await Task.Delay(100);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    Console.WriteLine("InitializeWebViewAsync: No hwnd");
                    return;
                }

                // 确保共享环境只创建一次
                if (_sharedEnvironment == null)
                {
                    Console.WriteLine("Creating shared WebView2 environment...");
                    _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions 
                    { 
                        AdditionalBrowserArguments = "--disable-gpu --disable-software-rasterizer --disable-dev-shm-usage --no-sandbox",
                        AllowSingleSignOnUsingOSPrimaryAccount = false
                    });
                    Console.WriteLine("Shared WebView2 environment created");
                }

                Console.WriteLine("Creating WebView2 controller...");
                _controller = await _sharedEnvironment.CreateCoreWebView2ControllerAsync(hwnd);
                Console.WriteLine("WebView2 controller created");

                _controller.IsVisible = false;
                _controller.CoreWebView2.Settings.IsScriptEnabled = true;

                _webview = _controller.CoreWebView2;
                _webview.NavigationCompleted += Webview_NavigationCompleted;
                _webview.WebMessageReceived += Webview_WebMessageReceived;

                _webview.Settings.AreDefaultContextMenusEnabled = true;
                _webview.Settings.IsZoomControlEnabled = true;

                _webview.AddHostObjectToScript("weaveDocHost", new WeaveDocHost(this));

                var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "preview-template.html");
                var html = File.ReadAllText(htmlPath);
                
                // 读取 KaTeX 文件并内联到 HTML 中
                var katexCssPath = Path.Combine(AppContext.BaseDirectory, "Assets", "katex", "katex.min.css");
                var katexCss = File.ReadAllText(katexCssPath);
                
                var katexJsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "katex", "katex.min.js");
                var katexJs = File.ReadAllText(katexJsPath);
                
                var katexRenderJsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "katex", "auto-render.min.js");
                var katexRenderJs = File.ReadAllText(katexRenderJsPath);
                
                // 内联 KaTeX CSS 和 JS
                html = html.Replace("<link rel=\"stylesheet\" href=\"katex/katex.min.css\">", 
                    $"<style>{katexCss}</style>");
                html = html.Replace("<script src=\"katex/katex.min.js\"></script>", 
                    $"<script>{katexJs}</script>");
                html = html.Replace("<script src=\"katex/auto-render.min.js\"></script>", 
                    $"<script>{katexRenderJs}</script>");
                
                UpdateControllerBounds();
                _controller.IsVisible = true;
                
                _webview.NavigateToString(html);
                Console.WriteLine("Preview WebView2 initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InitializeWebViewAsync exception: {ex.Message}");
                Logger.LogException(ex);
            }
        }

        private void Webview_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                _isInitialized = true;

                UpdateControllerBounds();

                // 执行测试脚本检查 JavaScript 环境
                TestJavaScriptEnvironment();

                if (!string.IsNullOrEmpty(_pendingContent))
                {
                    var contentToApply = _pendingContent;
                    _pendingContent = string.Empty;
                    UpdatePreview(contentToApply);
                }
            }
        }

        private async void TestJavaScriptEnvironment()
        {
            try
            {
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null in TestJavaScriptEnvironment");
                    return;
                }
                
                // 检查 window.weaveDocHost 是否存在
                var checkWeaveDocHost = await _webview.ExecuteScriptAsync("typeof window.weaveDocHost !== 'undefined'");
                Console.WriteLine($"window.weaveDocHost exists: {checkWeaveDocHost}");

                // 检查 window.external 是否存在
                var checkExternal = await _webview.ExecuteScriptAsync("typeof window.external !== 'undefined'");
                Console.WriteLine($"window.external exists: {checkExternal}");

                // 检查 window.chrome.webview 是否存在
                var checkChrome = await _webview.ExecuteScriptAsync("typeof window.chrome !== 'undefined' && typeof window.chrome.webview !== 'undefined'");
                Console.WriteLine($"window.chrome.webview exists: {checkChrome}");

                // 发送测试消息
                var testResult = await _webview.ExecuteScriptAsync(@"
                    (function() {
                        var result = {
                            weaveDocHost: typeof window.weaveDocHost !== 'undefined',
                            external: typeof window.external !== 'undefined',
                            chromeWebview: typeof window.chrome !== 'undefined' && typeof window.chrome.webview !== 'undefined'
                        };
                        return JSON.stringify(result);
                    })();
                ");
                Console.WriteLine($"JavaScript environment test result: {testResult}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestJavaScriptEnvironment exception: {ex.Message}");
            }
        }

        private void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            Console.WriteLine("Webview_WebMessageReceived called");
            try
            {
                var json = args.WebMessageAsJson;
                Console.WriteLine($"Received JSON: {json}");
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

                if (msgType == "debug" && msgData != null)
                {
                    Console.WriteLine($"PreviewWebViewControl: DEBUG - {msgData}");
                }
                else if (msgType == "previewSelection" && msgData != null)
                {
                    Console.WriteLine($"PreviewWebViewControl: Received previewSelection message");
                    try
                    {
                        var selectionData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = selectionData.RootElement;

                        int startLine = 1;
                        int startColumn = 1;
                        int endLine = 1;
                        int endColumn = 1;
                        int selectionLength = 1;

                        if (root.TryGetProperty("startLine", out var startLineProp))
                        {
                            startLine = startLineProp.GetInt32();
                        }
                        if (root.TryGetProperty("startColumn", out var startColProp))
                        {
                            startColumn = startColProp.GetInt32();
                        }
                        if (root.TryGetProperty("endLine", out var endLineProp))
                        {
                            endLine = endLineProp.GetInt32();
                        }
                        if (root.TryGetProperty("endColumn", out var endColProp))
                        {
                            endColumn = endColProp.GetInt32();
                        }
                        if (root.TryGetProperty("length", out var lengthProp))
                        {
                            selectionLength = lengthProp.GetInt32();
                        }

                        Console.WriteLine($"PreviewWebViewControl: Selection: startLine={startLine}, startColumn={startColumn}, endLine={endLine}, endColumn={endColumn}, length={selectionLength}");

                        var rootWindow = this.VisualRoot as Window;
                        
                        if (rootWindow is MainWindow mainWindow)
                        {
                            mainWindow.ScrollEditorToPositionWithRange(startLine, startColumn, selectionLength);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PreviewWebViewControl: Exception: {ex.Message}");
                        Logger.LogException(ex);
                    }
                }
                else if (msgType == "previewClick" && msgData != null)
                {
                    Console.WriteLine($"PreviewWebViewControl: Received previewClick message");
                    try
                    {
                        var clickData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = clickData.RootElement;

                        int clickedLine = 1;
                        int clickedColumn = 1;
                        if (root.TryGetProperty("line", out var lineProp))
                        {
                            clickedLine = lineProp.GetInt32();
                        }
                        if (root.TryGetProperty("column", out var colProp))
                        {
                            clickedColumn = colProp.GetInt32();
                        }

                        Console.WriteLine($"PreviewWebViewControl: line={clickedLine}, column={clickedColumn}");

                        var rootWindow = this.VisualRoot as Window;
                        Console.WriteLine($"PreviewWebViewControl: rootWindow is null: {rootWindow == null}");
                        Console.WriteLine($"PreviewWebViewControl: rootWindow is MainWindow: {rootWindow is MainWindow}");
                        
                        if (rootWindow is MainWindow mainWindow)
                        {
                            mainWindow.ScrollEditorToPosition(clickedLine, clickedColumn);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PreviewWebViewControl: Exception: {ex.Message}");
                        Logger.LogException(ex);
                    }
                }
                else if (msgType == "previewClearHighlight" && msgData != null)
                {
                    Console.WriteLine($"PreviewWebViewControl: Received previewClearHighlight message");
                    try
                    {
                        var rootWindow = this.VisualRoot as Window;
                        
                        if (rootWindow is MainWindow mainWindow)
                        {
                            mainWindow.ClearEditorHighlight();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"PreviewWebViewControl: Exception: {ex.Message}");
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

                // 检查内容是否真的变了
                if (_pendingContent == content)
                {
                    return;
                }

                // 内容变了，更新_pendingContent并执行脚本
                _pendingContent = content;

                // 如果已初始化，立即执行JavaScript更新
                if (_isInitialized)
                {
                    var script = $"window.updateContent({System.Text.Json.JsonSerializer.Serialize(content)});";
                    await _webview.ExecuteScriptAsync(script);

                    // 只有当内容不为空且包含 $ 符号时才进行 KaTeX 渲染
                    if (!string.IsNullOrEmpty(content) && content.Contains('$'))
                    {
                        await RenderLatexAsync();
                    }

                    // 内容更新完成后，通知外部刷新高亮
                    NotifyPreviewReady();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async Task RenderLatexAsync()
        {
            try
            {
                if (_webview == null)
                    return;

                var renderScript = @"
                    if (typeof katex !== 'undefined' && typeof renderMathInElement !== 'undefined') {
                        renderMathInElement(document.body, {
                            delimiters: [
                                { left: '$$', right: '$$', display: true },
                                { left: '$', right: '$', display: false }
                            ],
                            throwOnError: false
                        });
                    }
                ";

                await _webview.ExecuteScriptAsync(renderScript);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void NotifyPreviewReady()
        {
            if (_notifyPreviewReadyCallback != null)
            {
                Console.WriteLine("PreviewWebViewControl: Notifying preview ready");
                _notifyPreviewReadyCallback();
                _notifyPreviewReadyCallback = null;
            }
        }

        private Action? _notifyPreviewReadyCallback;

        public void SetOnPreviewReadyCallback(Action callback)
        {
            _notifyPreviewReadyCallback = callback;
        }

        public void HandlePreviewClick(int line, int column)
        {
            Console.WriteLine($"PreviewWebViewControl: HandlePreviewClick line={line}, column={column}");

            var rootWindow = this.VisualRoot as Window;
            if (rootWindow is MainWindow mainWindow)
            {
                mainWindow.ScrollEditorToPosition(line, column);
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
            catch
            {
            }
        }

        public void SetContent(string content)
        {
            HtmlContent = content;
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
                        if (window.clearHighlight) {{
                            window.clearHighlight();
                        }}
                        
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
                            targetElement.classList.add('highlight-line');
                            targetElement.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
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

        public async void ScrollToSelection(int startLine, int startCol, int endLine, int endCol)
        {
            try
            {
                Console.WriteLine($"ScrollToSelection called: startLine={startLine}, startCol={startCol}, endLine={endLine}, endCol={endCol}");
                
                // 确保预览器已初始化
                if (_webview == null || !_isInitialized)
                {
                    Console.WriteLine($"ScrollToSelection skipped: webview={_webview != null}, initialized={_isInitialized}");
                    return;
                }

                // 检查当前预览内容是否包含data-pos属性
                var dataPosCount = await _webview.ExecuteScriptAsync("document.querySelectorAll('[data-pos]').length");
                Console.WriteLine($"ScrollToSelection: Number of data-pos elements: {dataPosCount}");
                
                // 如果没有data-pos元素，等待内容更新
                if (dataPosCount == "0")
                {
                    Console.WriteLine("ScrollToSelection: No data-pos elements found, waiting for content update...");
                    await WaitForContentReadyAsync();
                    dataPosCount = await _webview.ExecuteScriptAsync("document.querySelectorAll('[data-pos]').length");
                    Console.WriteLine($"ScrollToSelection: After waiting, data-pos elements: {dataPosCount}");
                }
                
                // 等待JavaScript环境完全就绪
                await WaitForJavaScriptReadyAsync();
                
                var script = $"window.scrollToSelection({startLine}, {startCol}, {endLine}, {endCol});";
                Console.WriteLine($"ScrollToSelection executing script: {script}");
                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"ScrollToSelection script result: {result}");
                
                // 检查高亮是否生效
                var highlightedCount = await _webview.ExecuteScriptAsync("document.querySelectorAll('.highlight-char').length");
                Console.WriteLine($"ScrollToSelection: Number of highlighted elements after script: {highlightedCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScrollToSelection exception: {ex.Message}");
                Logger.LogException(ex);
            }
        }
        
        private async Task WaitForContentReadyAsync()
        {
            if (_webview == null) return;
            
            int attempts = 0;
            const int maxAttempts = 30;
            
            while (attempts < maxAttempts)
            {
                try
                {
                    var result = await _webview.ExecuteScriptAsync("document.querySelectorAll('[data-pos]').length");
                    if (result != "0")
                    {
                        Console.WriteLine("WaitForContentReadyAsync: Content is ready");
                        return;
                    }
                }
                catch
                {
                }
                
                attempts++;
                await Task.Delay(100);
            }
            
            Console.WriteLine("Warning: Content not ready after timeout");
        }
        
        private async Task WaitForJavaScriptReadyAsync()
        {
            if (_webview == null) return;
            
            int attempts = 0;
            const int maxAttempts = 20;
            
            while (attempts < maxAttempts)
            {
                try
                {
                    var result = await _webview.ExecuteScriptAsync("typeof window.scrollToSelection");
                    if (result == "\"function\"")
                    {
                        Console.WriteLine("JavaScript environment is ready");
                        return;
                    }
                }
                catch
                {
                }
                
                attempts++;
                await Task.Delay(50);
            }
            
            Console.WriteLine("Warning: JavaScript environment not fully ready after timeout");
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        
        public async Task Activate(bool forceReset = false)
        {
            if (_isActive) return;
            
            _isActive = true;
            Console.WriteLine("PreviewWebViewControl: Activating...");
            
            if (_controller != null && _webview != null)
            {
                UpdateControllerBounds();
                _controller.IsVisible = true;
                
                // 只有在强制重置或未初始化时才重新加载模板
                if (forceReset || !_isInitialized)
                {
                    Console.WriteLine("PreviewWebViewControl: Reloading preview template");
                    
                    _pendingContent = string.Empty;
                    
                    var rootWindow = VisualRoot as Window;
                    if (rootWindow is Views.MainWindow mainWindow && mainWindow.DataContext is ViewModels.MainWindowViewModel vm)
                    {
                        _pendingContent = vm.PreviewHtml;
                        Console.WriteLine($"PreviewWebViewControl: Will restore content length: {_pendingContent.Length}");
                    }
                    
                    _isInitialized = false;
                    
                    var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "preview-template.html");
                    var html = File.ReadAllText(htmlPath);
                    _webview.NavigateToString(html);
                }
                else
                {
                    Console.WriteLine("PreviewWebViewControl: Already initialized, showing existing content");
                }
            }
            else
            {
                _isInitialized = false;
                await InitializeWebViewAsync();
            }
        }
        
        public void Deactivate()
        {
            if (!_isActive) return;
            
            _isActive = false;
            Console.WriteLine("PreviewWebViewControl: Deactivating...");
            
            if (_controller != null)
            {
                try
                {
                    // 先关闭控制器，确保完全移除WebView2
                    _controller.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing preview controller: {ex.Message}");
                }
                _controller = null;
                _webview = null;
            }
            // 重置初始化标志，下次激活时重新初始化
            _isInitialized = false;
        }
    }
}
