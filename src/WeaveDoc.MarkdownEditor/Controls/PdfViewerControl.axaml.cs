using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.MarkdownEditor.Controls.Web;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class PdfViewerControl : UserControl
    {
        public const string DefaultFallbackStatusText =
            "PDF Reader 不可用：跨平台 WebView 未初始化。请确认系统 WebKit/WPE 运行库可用。";

        private IWebViewHost? _webViewHost;
        private string? _pendingFilePath;
        private bool _isActive;
        private bool _isFullScreen;
        private bool _isInitializing;
        private bool _isNavigationCompleted;
        // True once viewer.html has loaded and PDF.js is ready; avoids re-navigating on every tab switch.
        private bool _isViewerReady;
        private Window? _fullScreenWindow;
        private static HttpListener? _httpListener;
        private static int _serverPort;
        private static string? _currentPdfPath;
        private static readonly System.Collections.Generic.Dictionary<string, byte[]> _fileCache =
            new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);

        public static readonly StyledProperty<string> PdfFilePathProperty =
            AvaloniaProperty.Register<PdfViewerControl, string>(nameof(PdfFilePath));

        public static readonly StyledProperty<string> ViewerCssThemeProperty =
            AvaloniaProperty.Register<PdfViewerControl, string>(nameof(ViewerCssTheme), "Auto");

        public static readonly StyledProperty<bool> IsUsingFallbackProperty =
            AvaloniaProperty.Register<PdfViewerControl, bool>(nameof(IsUsingFallback), false);

        public static readonly StyledProperty<string> FallbackStatusTextProperty =
            AvaloniaProperty.Register<PdfViewerControl, string>(
                nameof(FallbackStatusText),
                DefaultFallbackStatusText);

        static PdfViewerControl()
        {
            ViewerCssThemeProperty.Changed.AddClassHandler<PdfViewerControl>(
                (control, _) => control.OnViewerCssThemeChanged());
        }

        public PdfViewerControl()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        public IWebViewHostFactory WebViewHostFactory { get; set; } = WebViewHostFactoryProvider.Current;

        public TimeSpan NavigationTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public string? PdfFilePath
        {
            get => _pendingFilePath;
            set
            {
                _pendingFilePath = value;
                SetValue(PdfFilePathProperty, value ?? string.Empty);
                if (_isActive && value != null)
                {
                    _ = LoadPdfAsync(value);
                }
            }
        }

        public string ViewerCssTheme
        {
            get => GetValue(ViewerCssThemeProperty);
            set => SetValue(ViewerCssThemeProperty, value);
        }

        public bool IsUsingFallback
        {
            get => GetValue(IsUsingFallbackProperty);
            set => SetValue(IsUsingFallbackProperty, value);
        }

        public string FallbackStatusText
        {
            get => GetValue(FallbackStatusTextProperty);
            set => SetValue(FallbackStatusTextProperty, value ?? DefaultFallbackStatusText);
        }

        public bool IsFullScreen => _isFullScreen;

        public event EventHandler? FullScreenChanged;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async Task InitializeAsync()
        {
            if (_isActive)
            {
                return;
            }

            _isActive = true;
            await EnsureWebViewAsync();

            if (_pendingFilePath != null)
            {
                await LoadPdfAsync(_pendingFilePath);
            }
        }

        private async void OnUnloaded(object? sender, EventArgs e)
        {
            await DisposeHostAsync();
        }

        public async Task LoadPdfAsync(string filePath)
        {
            _pendingFilePath = filePath;
            SetCurrentValue(PdfFilePathProperty, filePath ?? string.Empty);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowFallback("PDF 文件路径不能为空。");
                return;
            }

            if (!File.Exists(filePath))
            {
                ShowFallback($"PDF 文件不存在：{filePath}");
                return;
            }

            _currentPdfPath = filePath;

            if (!await EnsureWebViewAsync())
            {
                return;
            }

            if (_webViewHost == null)
            {
                return;
            }

            if (_serverPort <= 0)
            {
                ShowFallback("PDF Reader 不可用：本地 PDF.js 服务未启动。");
                return;
            }

            // 主犯1修复：viewer.html 已经加载过，直接用 JS 打开新 PDF，不重新 Navigate
            if (_isViewerReady)
            {
                try
                {
                    await _webViewHost.InvokeScriptAsync(BuildPdfOpenScript());
                    await ApplyPdfViewerThemeAsync();
                    _webViewHost.View.IsVisible = _isActive;
                    ClearFallback();
                    ApplyMarginTweak();
                }
                catch (Exception ex)
                {
                    ShowFallback($"PDF Reader 不可用：{ex.Message}");
                }
                return;
            }

            // viewer.html 未加载：完整 Navigate
            var viewerUrl = BuildViewerUrl(_serverPort, GetNormalizedViewerCssTheme());
            try
            {
                _isNavigationCompleted = false;
                _webViewHost.Navigate(new Uri(viewerUrl));
                _ = ShowFallbackIfNavigationDoesNotCompleteAsync(_webViewHost, filePath);
                if (!IsUsingFallback)
                {
                    _webViewHost.View.IsVisible = _isActive;
                    ClearFallback();
                }
            }
            catch (Exception ex)
            {
                ShowFallback($"PDF Reader 不可用：{ex.Message}");
            }
        }

        private async Task ShowFallbackIfNavigationDoesNotCompleteAsync(IWebViewHost host, string filePath)
        {
            if (NavigationTimeout <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(NavigationTimeout).ConfigureAwait(true);

            if (_webViewHost == host
                && !_isNavigationCompleted
                && string.Equals(_pendingFilePath, filePath, StringComparison.Ordinal))
            {
                ShowFallback("PDF Reader 不可用：PDF.js 导航超时。");
            }
        }

        public static string BuildViewerUrl(int serverPort)
        {
            return $"http://localhost:{serverPort}/pdfjs-5.7.284-dist/web/viewer.html?file=/pdf/current";
        }

        public static string BuildViewerUrl(int serverPort, string viewerCssTheme)
        {
            var normalizedTheme = NormalizeViewerCssTheme(viewerCssTheme);
            return $"http://localhost:{serverPort}/pdfjs-5.7.284-dist/web/viewer.html?file=/pdf/current&weavedocTheme={normalizedTheme}";
        }

        private static string NormalizeViewerCssTheme(string? viewerCssTheme)
        {
            return viewerCssTheme?.Trim().ToLowerInvariant() switch
            {
                "dark" => "dark",
                "light" => "light",
                _ => "auto"
            };
        }

        private string GetNormalizedViewerCssTheme()
        {
            return NormalizeViewerCssTheme(ViewerCssTheme);
        }

        private void OnViewerCssThemeChanged()
        {
            if (_webViewHost == null || !_isViewerReady)
            {
                return;
            }

            _ = ApplyPdfViewerThemeAsync();
        }

        private async Task ApplyPdfViewerThemeAsync()
        {
            if (_webViewHost == null)
            {
                return;
            }

            var theme = GetNormalizedViewerCssTheme();
            var script = $$"""
                (() => {
                    const theme = "{{theme}}";
                    const prefValue = theme === "light" ? 1 : theme === "dark" ? 2 : 0;
                    try {
                        const existing = JSON.parse(localStorage.getItem("pdfjs.preferences") || "{}");
                        const prefs = existing && typeof existing === "object" ? existing : {};
                        prefs.viewerCssTheme = prefValue;
                        localStorage.setItem("pdfjs.preferences", JSON.stringify(prefs));
                    } catch {
                    }
                    if (theme === "light" || theme === "dark") {
                        document.documentElement.style.colorScheme = theme;
                    } else {
                        document.documentElement.style.removeProperty("color-scheme");
                    }
                    return theme;
                })();
                """;

            try
            {
                await _webViewHost.InvokeScriptAsync(script);
            }
            catch
            {
                // Theme synchronization is visual-only; keep the loaded PDF usable.
            }
        }

        public static string BuildPdfJsCompatibilityScript()
        {
            return """
                (() => {
                    const post = (type, data) => {
                        try {
                            globalThis.weaveDocBridge?.post({ Type: type, Data: data });
                        } catch {
                        }
                    };

                    const normalizeWeaveDocTheme = value => {
                        value = String(value || "").toLowerCase();
                        if (value === "light") return "light";
                        if (value === "dark") return "dark";
                        return "auto";
                    };

                    const applyWeaveDocThemePreference = () => {
                        const params = new URLSearchParams(globalThis.location.search);
                        const theme = normalizeWeaveDocTheme(params.get("weavedocTheme"));
                        const prefValue = theme === "light" ? 1 : theme === "dark" ? 2 : 0;
                        try {
                            const existing = JSON.parse(localStorage.getItem("pdfjs.preferences") || "{}");
                            const prefs = existing && typeof existing === "object" ? existing : {};
                            prefs.viewerCssTheme = prefValue;
                            localStorage.setItem("pdfjs.preferences", JSON.stringify(prefs));
                        } catch {
                        }
                        if (theme === "light" || theme === "dark") {
                            document.documentElement.style.colorScheme = theme;
                        } else {
                            document.documentElement.style.removeProperty("color-scheme");
                        }
                    };

                    applyWeaveDocThemePreference();

                    if (!globalThis.__weaveDocConsoleBridgeAttached) {
                        globalThis.__weaveDocConsoleBridgeAttached = true;
                        const originalError = console.error.bind(console);
                        const originalWarn = console.warn.bind(console);

                        console.error = (...args) => {
                            post("pdfjs-console", `error: ${args.map(String).join(" ")}`);
                            originalError(...args);
                        };

                        console.warn = (...args) => {
                            post("pdfjs-console", `warn: ${args.map(String).join(" ")}`);
                            originalWarn(...args);
                        };

                        globalThis.addEventListener("error", event => {
                            post("pdfjs-console", `window error: ${event.message}`);
                        });

                        globalThis.addEventListener("unhandledrejection", event => {
                            post("pdfjs-console", `unhandled rejection: ${event.reason?.message ?? event.reason}`);
                        });
                    }

                    if (typeof URL !== "undefined" && typeof URL.parse !== "function") {
                        URL.parse = (url, base) => {
                            try {
                                return new URL(url, base);
                            } catch {
                                return null;
                            }
                        };
                    }

                    if (typeof Promise !== "undefined" && typeof Promise.try !== "function") {
                        Promise.try = (callback, ...args) => new Promise(resolve => resolve()).then(() => callback(...args));
                    }

                    if (typeof Uint8Array !== "undefined" && typeof Uint8Array.prototype.toHex !== "function") {
                        Uint8Array.prototype.toHex = function () {
                            return Array.from(this, byte => byte.toString(16).padStart(2, "0")).join("");
                        };
                    }

                    if (typeof Map !== "undefined" && typeof Map.prototype.getOrInsertComputed !== "function") {
                        Map.prototype.getOrInsertComputed = function (key, callback) {
                            if (this.has(key)) {
                                return this.get(key);
                            }

                            const value = callback(key);
                            this.set(key, value);
                            return value;
                        };
                    }
                })();
                """;
        }

        public static string BuildPdfWorkerCompatibilityPrefix()
        {
            return """
                if (typeof Promise !== "undefined" && typeof Promise.try !== "function") {
                    Promise.try = (callback, ...args) => new Promise(resolve => resolve()).then(() => callback(...args));
                }

                if (typeof Uint8Array !== "undefined" && typeof Uint8Array.prototype.toHex !== "function") {
                    Uint8Array.prototype.toHex = function () {
                        return Array.from(this, byte => byte.toString(16).padStart(2, "0")).join("");
                    };
                }

                if (typeof Map !== "undefined" && typeof Map.prototype.getOrInsertComputed !== "function") {
                    Map.prototype.getOrInsertComputed = function (key, callback) {
                        if (this.has(key)) {
                            return this.get(key);
                        }

                        const value = callback(key);
                        this.set(key, value);
                        return value;
                    };
                }

                """;
        }

        public static string BuildPdfOpenScript()
        {
            return """
                (() => {
                    const post = (data) => {
                        try {
                            globalThis.weaveDocBridge?.post({ Type: "pdfjs-open", Data: data });
                        } catch {
                        }
                    };

                    const summarizeEvent = event => {
                        if (!event) {
                            return {};
                        }

                        return {
                            pageNumber: event.pageNumber ?? event.page?.pageNumber ?? null,
                            pagesCount: event.pagesCount ?? null,
                        };
                    };

                    const enableTextSelection = reason => {
                        if (!document.getElementById("weavedoc-pdf-text-selection-style")) {
                            const style = document.createElement("style");
                            style.id = "weavedoc-pdf-text-selection-style";
                            style.textContent = `
                                #viewerContainer,
                                #viewer,
                                .pdfViewer,
                                .pdfViewer .page,
                                .pdfViewer .textLayer,
                                .pdfViewer .textLayer span,
                                .pdfViewer .textLayer br {
                                    -webkit-user-select: text !important;
                                    user-select: text !important;
                                }

                                .pdfViewer .textLayer {
                                    pointer-events: auto !important;
                                    z-index: 2 !important;
                                }

                                .pdfViewer .textLayer span,
                                .pdfViewer .textLayer br {
                                    pointer-events: auto !important;
                                    cursor: text !important;
                                }
                            `;
                            document.head.appendChild(style);
                        }

                        document.documentElement.classList.remove("grab-to-pan-grab", "grab-to-pan-grabbing");
                        document.body.classList.remove("grab-to-pan-grab", "grab-to-pan-grabbing");
                        document.getElementById("cursorSelectTool")?.click();

                        const layers = document.querySelectorAll(".textLayer");
                        const spans = document.querySelectorAll(".textLayer span");
                        for (const layer of layers) {
                            layer.style.pointerEvents = "auto";
                            layer.style.userSelect = "text";
                            layer.style.webkitUserSelect = "text";
                        }
                        for (const span of spans) {
                            span.style.pointerEvents = "auto";
                            span.style.userSelect = "text";
                            span.style.webkitUserSelect = "text";
                            span.style.cursor = "text";
                        }

                        post(`text selection ${reason}: layers=${layers.length}, spans=${spans.length}`);
                    };

                    let attempts = 0;
                    const openWhenReady = () => {
                        attempts += 1;
                        const app = globalThis.PDFViewerApplication;

                        if (!app || typeof app.open !== "function") {
                            if (attempts >= 100) {
                                post("open failed: PDFViewerApplication unavailable");
                                return;
                            }

                            post(`waiting for PDFViewerApplication (${attempts})`);
                            setTimeout(openWhenReady, 50);
                            return;
                        }

                        if (!app.initialized) {
                            if (attempts >= 100) {
                                post("open failed: PDFViewerApplication initialization timeout");
                                return;
                            }

                            post(`waiting for PDFViewerApplication initialization (${attempts})`);
                            setTimeout(openWhenReady, 50);
                            return;
                        }

                        if (!globalThis.__weaveDocPdfEventsAttached) {
                            globalThis.__weaveDocPdfEventsAttached = true;
                            const events = ["documentloaded", "pagesinit", "pagesloaded", "pagerendered", "textlayerrendered", "pagechanging"];
                            for (const eventName of events) {
                                app.eventBus?._on(eventName, event => {
                                    post(`${eventName}: ${JSON.stringify(summarizeEvent(event))}`);
                                    if (eventName === "pagesloaded" || eventName === "pagerendered" || eventName === "textlayerrendered") {
                                        setTimeout(() => enableTextSelection(eventName), 0);
                                    }
                                });
                            }
                        }

                        enableTextSelection("before open");
                        // 主犯2修复：用 URL 模式让 PDF.js 自行流式加载（支持 Range 请求按需取页），
                        // 不再把整个文件 fetch 成 ArrayBuffer 再传入。
                        const url = new URL("/pdf/current", globalThis.location.href).href;
                        post(`opening via url: ${url}`);
                        app.open({
                            url: url,
                            cMapUrl: "./cmaps/",
                            cMapPacked: true,
                            enableXfa: false,
                            verbosity: 0
                        })
                        .then(() => {
                            post("open completed");
                            setTimeout(() => window.dispatchEvent(new Event('resize')), 150);
                            setTimeout(() => enableTextSelection("open completed"), 0);
                            setTimeout(() => enableTextSelection("open completed delayed"), 500);
                        })
                        .catch(error => post(`open failed: ${error?.message ?? error}`));
                    };

                    openWhenReady();
                    return "PDF open polling started";
                })();
                """;
        }

        private bool _isWindowOpened = false;
        private Window? _attachedWindow;
        private bool _pendingMarginTweak = false;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                _attachedWindow = window;
                _isWindowOpened = window.IsVisible;
                window.Opened += Window_Opened;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_attachedWindow != null)
            {
                _attachedWindow.Opened -= Window_Opened;
                _attachedWindow = null;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void Window_Opened(object? sender, EventArgs e)
        {
            _isWindowOpened = true;
            if (_pendingMarginTweak)
            {
                _pendingMarginTweak = false;
                ApplyMarginTweakCore();
            }
        }

        private void ApplyMarginTweak()
        {
            if (!_isWindowOpened)
            {
                _pendingMarginTweak = true;
                return;
            }
            ApplyMarginTweakCore();
        }

        private void ApplyMarginTweakCore()
        {
            Dispatcher.UIThread.Post(async () =>
            {
                if (_webViewHost == null) return;
                var originalMargin = _webViewHost.View.Margin;
                _webViewHost.View.Margin = new Avalonia.Thickness(
                    originalMargin.Left, originalMargin.Top, originalMargin.Right, originalMargin.Bottom + 1.0);
                _webViewHost.View.InvalidateMeasure();
                _webViewHost.View.InvalidateArrange();
                await Task.Delay(150);
                if (_webViewHost != null)
                {
                    _webViewHost.View.Margin = originalMargin;
                    _webViewHost.View.InvalidateMeasure();
                    _webViewHost.View.InvalidateArrange();
                }
            });
        }

        private async Task<bool> EnsureWebViewAsync()
        {
            if (_webViewHost != null)
            {
                return true;
            }

            if (_isInitializing)
            {
                return false;
            }

            _isInitializing = true;
            try
            {
                if (!StartHttpServer(out var serverError))
                {
                    ShowFallback($"PDF Reader 不可用：{serverError}");
                    return false;
                }

                _webViewHost = WebViewHostFactory.Create();
                _webViewHost.NavigationCompleted += PdfWebView_NavigationCompleted;
                _webViewHost.MessageReceived += PdfWebView_WebMessageReceived;
                GetMainGrid().Children.Add(_webViewHost.View);
                _webViewHost.View.IsVisible = _isActive;

                // Wait for Avalonia to complete a layout pass and for the GTK native
                // handle to be fully realized before any navigation attempt.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

                ClearFallback();
                return true;
            }
            catch (Exception ex)
            {
                await DisposeHostAsync();
                ShowFallback($"PDF Reader 不可用：{ex.Message}");
                return false;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void PdfWebView_WebMessageReceived(object? sender, WebViewHostMessageReceivedEventArgs e)
        {
            Console.WriteLine($"PDF WebView message: {e.Body}");
            if (e.Body.Contains("open failed", StringComparison.OrdinalIgnoreCase)
                || e.Body.Contains("window error", StringComparison.OrdinalIgnoreCase)
                || e.Body.Contains("unhandled rejection", StringComparison.OrdinalIgnoreCase))
            {
                ShowFallback($"PDF Reader 不可用：{e.Body}");
            }
        }

        private async void PdfWebView_NavigationCompleted(object? sender, WebViewHostNavigationCompletedEventArgs e)
        {
            _isNavigationCompleted = e.IsSuccess;

            if (!e.IsSuccess || _webViewHost == null || string.IsNullOrEmpty(_currentPdfPath))
            {
                if (!e.IsSuccess)
                {
                    ShowFallback("PDF Reader 不可用：PDF.js 导航失败。");
                }
                return;
            }

            if (ShowFallbackIfNativeRenderingUnavailable())
            {
                return;
            }

            try
            {
                await _webViewHost.InvokeScriptAsync(BuildPdfOpenScript());
                await ApplyPdfViewerThemeAsync();
                // viewer.html 已就绪，后续切换 Tab 可直接复用，无需重新 Navigate
                _isViewerReady = true;
                ClearFallback();
                ApplyMarginTweak();
            }
            catch (Exception ex)
            {
                ShowFallback($"PDF Reader 不可用：{ex.Message}");
            }
        }

        private bool StartHttpServer(out string? errorMessage)
        {
            errorMessage = null;
            if (_httpListener != null)
            {
                if (_httpListener.IsListening)
                {
                    return true;
                }

                errorMessage = "本地 PDF.js 服务未运行。";
                return false;
            }

            try
            {
                _httpListener = new HttpListener();
                _serverPort = GetAvailablePort();
                var prefix = $"http://localhost:{_serverPort}/";
                _httpListener.Prefixes.Add(prefix);
                _httpListener.Start();

                _ = Task.Run(async () =>
                {
                    while (_httpListener.IsListening)
                    {
                        try
                        {
                            var context = await _httpListener.GetContextAsync();
                            await ProcessHttpRequest(context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"HTTP server error: {ex.Message}");
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"本地 PDF.js 服务启动失败：{ex.Message}";
                Console.WriteLine($"Failed to start HTTP server: {ex.Message}");
                try
                {
                    _httpListener?.Close();
                }
                catch
                {
                }

                _httpListener = null;
                _serverPort = 0;
                return false;
            }
        }

        private static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Port ?? 8080;
        }

        private static async Task ProcessHttpRequest(HttpListenerContext context)
        {
            try
            {
                var requestPath = context.Request.Url?.AbsolutePath ?? "/";
                var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
                if (!Directory.Exists(assetsDir))
                {
                    assetsDir = Path.Combine(AppContext.BaseDirectory, "src", "Assets");
                }

                string filePath;

                if (requestPath == "/pdf/current")
                {
                    filePath = _currentPdfPath ?? string.Empty;
                }
                else if (requestPath.StartsWith("/pdf/", StringComparison.Ordinal))
                {
                    var pdfPath = requestPath[5..];
                    filePath = Uri.UnescapeDataString(pdfPath);
                }
                else
                {
                    filePath = Path.Combine(assetsDir, requestPath.TrimStart('/'));
                }

                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Not found"));
                    context.Response.Close();
                    return;
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".html" => "text/html",
                    ".js" => "application/javascript",
                    ".mjs" => "application/javascript",
                    ".css" => "text/css",
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".svg" => "image/svg+xml",
                    ".gif" => "image/gif",
                    ".bcmap" => "application/octet-stream",
                    _ => "application/octet-stream"
                };

                context.Response.ContentType = contentType;
                context.Response.AddHeader("Access-Control-Allow-Origin", "*");

                byte[] fileBytes;
                if (!_fileCache.TryGetValue(filePath, out fileBytes!))
                {
                    fileBytes = await File.ReadAllBytesAsync(filePath);
                    // Only cache static assets (not the live PDF file)
                    if (requestPath != "/pdf/current" && !requestPath.StartsWith("/pdf/", StringComparison.Ordinal))
                    {
                        _fileCache[filePath] = fileBytes;
                    }
                }
                if (requestPath.EndsWith("/web/viewer.html", StringComparison.OrdinalIgnoreCase))
                {
                    fileBytes = InjectPdfViewerCompatibility(fileBytes);
                }
                else if (requestPath.EndsWith("/build/pdf.worker.mjs", StringComparison.OrdinalIgnoreCase))
                {
                    var prefixBytes = Encoding.UTF8.GetBytes(BuildPdfWorkerCompatibilityPrefix());
                    var patchedBytes = new byte[prefixBytes.Length + fileBytes.Length];
                    Buffer.BlockCopy(prefixBytes, 0, patchedBytes, 0, prefixBytes.Length);
                    Buffer.BlockCopy(fileBytes, 0, patchedBytes, prefixBytes.Length, fileBytes.Length);
                    fileBytes = patchedBytes;
                }

                // 主犯2修复：对 PDF 文件支持 Range 请求，让 PDF.js 可以按需懒加载页面
                var isPdfContent = requestPath == "/pdf/current"
                    || requestPath.StartsWith("/pdf/", StringComparison.Ordinal);
                if (isPdfContent)
                {
                    context.Response.AddHeader("Accept-Ranges", "bytes");
                    var rangeHeader = context.Request.Headers["Range"];
                    if (!string.IsNullOrEmpty(rangeHeader))
                    {
                        await ServeRangeResponseAsync(context, fileBytes, rangeHeader);
                        return;
                    }
                    // 全量响应时也给出 Content-Length，PDF.js 需要它来计算文件大小
                    context.Response.ContentLength64 = fileBytes.Length;
                }

                await context.Response.OutputStream.WriteAsync(fileBytes);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
                context.Response.StatusCode = 500;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Internal error"));
                context.Response.Close();
            }
        }

        /// <summary>
        /// 处理 Range 请求（HTTP 206 Partial Content），使 PDF.js 可按需拉取当前页的字节范围。
        /// </summary>
        private static async Task ServeRangeResponseAsync(HttpListenerContext context, byte[] data, string rangeHeader)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                rangeHeader, @"bytes=(\d+)-(\d*)");

            if (!match.Success)
            {
                context.Response.StatusCode = 416; // Range Not Satisfiable
                context.Response.AddHeader("Content-Range", $"bytes */{data.Length}");
                context.Response.Close();
                return;
            }

            long start = long.Parse(match.Groups[1].Value);
            long end = string.IsNullOrEmpty(match.Groups[2].Value)
                ? data.Length - 1
                : Math.Min(long.Parse(match.Groups[2].Value), data.Length - 1);

            if (start > end || start >= data.Length)
            {
                context.Response.StatusCode = 416;
                context.Response.AddHeader("Content-Range", $"bytes */{data.Length}");
                context.Response.Close();
                return;
            }

            long length = end - start + 1;
            context.Response.StatusCode = 206;
            context.Response.ContentType = "application/pdf";
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Accept-Ranges", "bytes");
            context.Response.AddHeader("Content-Range", $"bytes {start}-{end}/{data.Length}");
            context.Response.ContentLength64 = length;
            await context.Response.OutputStream.WriteAsync(data, (int)start, (int)length);
            context.Response.Close();
        }

        private static byte[] InjectPdfViewerCompatibility(byte[] fileBytes)

        {
            var html = Encoding.UTF8.GetString(fileBytes);
            if (html.Contains("__weaveDocPdfViewerCompatibilityInjected", StringComparison.Ordinal))
            {
                return fileBytes;
            }

            var injectedScript = $"""
                <script>
                globalThis.__weaveDocPdfViewerCompatibilityInjected = true;
                {WebViewBridge.Script}
                {BuildPdfJsCompatibilityScript()}
                </script>
                """;

            var insertionPoint = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            var patchedHtml = insertionPoint >= 0
                ? html.Insert(insertionPoint, injectedScript)
                : injectedScript + html;

            return Encoding.UTF8.GetBytes(patchedHtml);
        }

        public async Task Activate()
        {
            if (_isActive)
            {
                return;
            }

            _isActive = true;
            if (!await EnsureWebViewAsync())
            {
                return;
            }

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = true;

                // 等 Avalonia Render 优先级的布局传递完成，确保 GTK offscreen 拿到正确 viewport
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

                if (ShowFallbackIfNativeRenderingUnavailable())
                {
                    return;
                }

                // 主犯1修复：viewer.html 已就绪，切回 Tab 只需触发 resize，不重新加载
                if (_isViewerReady)
                {
                    try
                    {
                        await _webViewHost.InvokeScriptAsync("window.dispatchEvent(new Event('resize'));");
                    }
                    catch { }
                    return;
                }
            }

            if (_pendingFilePath != null)
            {
                await LoadPdfAsync(_pendingFilePath);
            }
        }

        public async Task DeactivateAsync()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = false;
            }

            await Task.CompletedTask;
        }

        public async Task ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                ExitFullScreen();
            }
            else
            {
                await EnterFullScreen();
            }
        }

        private async Task EnterFullScreen()
        {
            if (_pendingFilePath == null)
            {
                return;
            }

            _isFullScreen = true;
            FullScreenChanged?.Invoke(this, EventArgs.Empty);

            _fullScreenWindow = new Window
            {
                WindowState = WindowState.FullScreen,
                Title = "PDF Full Screen",
                Background = Brushes.Black
            };

            _fullScreenWindow.KeyDown += FullScreenWindow_KeyDown;
            var fullScreenViewer = new PdfViewerControl();
            _fullScreenWindow.Content = fullScreenViewer;
            _fullScreenWindow.Show();

            await fullScreenViewer.LoadPdfAsync(_pendingFilePath);
            await fullScreenViewer.Activate();

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = false;
            }
        }

        private void ExitFullScreen()
        {
            if (_fullScreenWindow != null)
            {
                _fullScreenWindow.KeyDown -= FullScreenWindow_KeyDown;
                _fullScreenWindow.Close();
                _fullScreenWindow = null;
            }

            _isFullScreen = false;
            FullScreenChanged?.Invoke(this, EventArgs.Empty);

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = _isActive;
            }
        }

        private void FullScreenWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitFullScreen();
            }
        }

        private void ShowFallback(string? statusText = null)
        {
            FallbackStatusText = string.IsNullOrWhiteSpace(statusText)
                ? DefaultFallbackStatusText
                : statusText;
            IsUsingFallback = true;

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = false;
            }
        }

        private void ClearFallback()
        {
            IsUsingFallback = false;
            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = _isActive;
            }
        }

        private bool ShowFallbackIfNativeRenderingUnavailable()
        {
            if (!WebViewRenderPolicy.ShouldUseFallback(_webViewHost))
            {
                return false;
            }

            ShowFallback(WebViewRenderPolicy.BuildFallbackStatus("PDF Reader"));
            return true;
        }

        private async Task DisposeHostAsync()
        {
            if (_webViewHost == null)
            {
                return;
            }

            _webViewHost.NavigationCompleted -= PdfWebView_NavigationCompleted;
            _webViewHost.MessageReceived -= PdfWebView_WebMessageReceived;
            TryGetMainGrid()?.Children.Remove(_webViewHost.View);
            await _webViewHost.DisposeAsync();
            _webViewHost = null;
            _isNavigationCompleted = false;
            _isViewerReady = false;
        }

        private Grid GetMainGrid()
        {
            return TryGetMainGrid()
                ?? throw new InvalidOperationException("PDF Reader 不可用：WebView 容器未初始化。");
        }

        private Grid? TryGetMainGrid()
        {
            return MainGrid ?? this.FindControl<Grid>("MainGrid");
        }
    }
}
