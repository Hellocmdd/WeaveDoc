using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class PdfViewerControl : UserControl
    {
        private CoreWebView2Controller? _controller;
        private CoreWebView2? _webview;
        private string? _pendingFilePath;
        private bool _isActive;
        private bool _isFullScreen;
        private Window? _fullScreenWindow;
        private static CoreWebView2Environment? _sharedEnvironment;
        private static HttpListener? _httpListener;
        private static int _serverPort = 0;
        private static string? _currentPdfPath;

        public static readonly StyledProperty<string> PdfFilePathProperty =
            AvaloniaProperty.Register<PdfViewerControl, string>(nameof(PdfFilePath));

        public string? PdfFilePath
        {
            get => _pendingFilePath;
            set
            {
                _pendingFilePath = value;
                if (_isActive && value != null)
                {
                    _ = LoadPdfAsync(value);
                }
            }
        }

        public bool IsFullScreen => _isFullScreen;

        public event EventHandler? FullScreenChanged;

        public PdfViewerControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            // 在Loaded时不自动激活，等待标签切换时再激活
        }

        public async Task InitializeAsync()
        {
            if (_isActive)
                return;

            _isActive = true;
            await InitializeWebViewAsync();
            
            if (_pendingFilePath != null)
            {
                await LoadPdfAsync(_pendingFilePath);
            }
        }

        private async void OnUnloaded(object? sender, EventArgs e)
        {
            await DeactivateAsync();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isActive)
            {
                UpdateBounds();
            }
        }

        public async Task LoadPdfAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            _pendingFilePath = filePath;
            _currentPdfPath = filePath;

            if (_controller == null)
            {
                await InitializeWebViewAsync();
            }

            // 如果控制器存在但位置不正确，先更新位置
            if (_controller != null)
            {
                UpdateBounds();
            }

            if (_webview != null && _serverPort > 0)
            {
                // 使用HTTP服务器加载PDF.js外壳，待页面初始化完成后再主动打开当前PDF。
                string viewerUrl = BuildViewerUrl(_serverPort);
                Console.WriteLine($"Loading PDF via HTTP server: {viewerUrl}");
                _webview.Navigate(viewerUrl);
                
                // 显示WebView2（调用此方法时应该已经激活）
                if (_controller != null)
                {
                    _controller.IsVisible = true;
                }
            }
            else if (_webview != null)
            {
                // 降级到直接导航
                string fileUri = "file:///" + filePath.Replace("\\", "/");
                _webview.Navigate(fileUri);
                
                // 显示WebView2
                if (_controller != null)
                {
                    _controller.IsVisible = true;
                }
            }
        }

        public static string BuildViewerUrl(int serverPort)
        {
            // 使用HTTP服务器的 /pdf/current 端点获取当前PDF文件
            return $"http://localhost:{serverPort}/pdfjs-5.7.284-dist/web/viewer.html?file=/pdf/current";
        }

        public static string BuildPdfJsCompatibilityScript()
        {
            return """
                (() => {
                    const post = (type, data) => {
                        try {
                            globalThis.chrome?.webview?.postMessage({ type, data });
                        } catch {
                        }
                    };

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
                            globalThis.chrome?.webview?.postMessage({ type: "pdfjs-open", data });
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
                            post(`waiting for PDFViewerApplication (${attempts})`);
                            if (attempts < 100) {
                                setTimeout(openWhenReady, 50);
                            }
                            return;
                        }

                        if (!app.initialized) {
                            post(`waiting for PDFViewerApplication initialization (${attempts})`);
                            if (attempts < 100) {
                                setTimeout(openWhenReady, 50);
                            }
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
                        const url = new URL("/pdf/current", globalThis.location.href).href;
                        post(`fetching ${url}`);
                        fetch(url, { cache: "no-store" })
                            .then(response => {
                                post(`fetch status ${response.status}`);
                                if (!response.ok) {
                                    throw new Error(`HTTP ${response.status}`);
                                }
                                return response.arrayBuffer();
                            })
                            .then(buffer => {
                                post(`opening PDF bytes ${buffer.byteLength}`);
                                return app.open({
                                    data: new Uint8Array(buffer),
                                    filename: "current.pdf",
                                    cMapUrl: "./cmaps/",
                                    cMapPacked: true,
                                    enableXfa: false,
                                    verbosity: 0
                                });
                            })
                            .then(() => {
                                post("open completed");
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

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var root = VisualRoot as Window;
                if (root == null)
                    return;

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                // 启动本地 HTTP 服务器
                StartHttpServer();

                if (_sharedEnvironment == null)
                {
                    _sharedEnvironment = await WebView2EnvironmentManager.GetOrCreateEnvironmentAsync2();
                    Console.WriteLine("Shared PDF WebView2 environment created");
                }

                _controller = await _sharedEnvironment.CreateCoreWebView2ControllerAsync(hwnd);
                _webview = _controller.CoreWebView2;
                await _webview.AddScriptToExecuteOnDocumentCreatedAsync(BuildPdfJsCompatibilityScript());
                _webview.NavigationCompleted += PdfWebView_NavigationCompleted;
                _webview.WebMessageReceived += PdfWebView_WebMessageReceived;

                // 启用右键菜单
                _webview.Settings.AreDefaultContextMenusEnabled = true;

                // 初始化时始终设置为不可见，由 Activate 方法控制显示时机
                _controller.IsVisible = false;
                
                // 设置一个初始的小尺寸和远离可见区域的位置，防止第一次初始化时意外显示
                _controller.Bounds = new System.Drawing.Rectangle(-1000, -1000, 100, 100);

                Console.WriteLine("PDF WebView2 initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize PDF WebView2: {ex.Message}");
            }
        }

        private void PdfWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Console.WriteLine($"PDF WebView2 message: {e.WebMessageAsJson}");
        }

        private async void PdfWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Console.WriteLine($"PDF WebView2 NavigationCompleted: success={e.IsSuccess}, status={e.HttpStatusCode}, error={e.WebErrorStatus}");

            if (!e.IsSuccess || _webview == null || string.IsNullOrEmpty(_currentPdfPath))
                return;

            try
            {
                var result = await _webview.ExecuteScriptAsync(BuildPdfOpenScript());
                Console.WriteLine($"PDF.js open result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute PDF.js open script: {ex.Message}");
            }
        }

        private void StartHttpServer()
        {
            if (_httpListener != null)
                return;

            try
            {
                _httpListener = new HttpListener();
                _serverPort = GetAvailablePort();
                string prefix = $"http://localhost:{_serverPort}/";
                _httpListener.Prefixes.Add(prefix);
                _httpListener.Start();
                Console.WriteLine($"HTTP server started on port {_serverPort}");

                // 在后台处理请求
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start HTTP server: {ex.Message}");
            }
        }

        private int GetAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Port ?? 8080;
            }
        }

        private async Task ProcessHttpRequest(HttpListenerContext context)
        {
            try
            {
                string requestPath = context.Request.Url?.AbsolutePath ?? "/";
                string assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
                if (!Directory.Exists(assetsDir))
                {
                    assetsDir = Path.Combine(AppContext.BaseDirectory, "src", "Assets");
                }

                string filePath;

                if (requestPath == "/pdf/current")
                {
                    filePath = _currentPdfPath ?? string.Empty;
                    Console.WriteLine($"PDF request: {requestPath} -> {filePath}");
                }
                else if (requestPath.StartsWith("/pdf/"))
                {
                    // PDF文件请求 - 获取URL解码后的路径
                    string pdfPath = requestPath.Substring(5);
                    filePath = Uri.UnescapeDataString(pdfPath);
                    Console.WriteLine($"PDF request: {requestPath} -> {filePath}");
                }
                else
                {
                    // 静态文件请求
                    filePath = Path.Combine(assetsDir, requestPath.TrimStart('/'));
                }

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    context.Response.StatusCode = 404;
                    await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Not found"));
                    context.Response.Close();
                    return;
                }

                // 设置正确的MIME类型
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                string contentType = extension switch
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

                byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                if (requestPath.EndsWith("/build/pdf.worker.mjs", StringComparison.OrdinalIgnoreCase))
                {
                    var prefixBytes = System.Text.Encoding.UTF8.GetBytes(BuildPdfWorkerCompatibilityPrefix());
                    var patchedBytes = new byte[prefixBytes.Length + fileBytes.Length];
                    Buffer.BlockCopy(prefixBytes, 0, patchedBytes, 0, prefixBytes.Length);
                    Buffer.BlockCopy(fileBytes, 0, patchedBytes, prefixBytes.Length, fileBytes.Length);
                    fileBytes = patchedBytes;
                    Console.WriteLine("Applied PDF worker compatibility prefix");
                }

                Console.WriteLine($"Serving file: {filePath}, size: {fileBytes.Length} bytes");
                await context.Response.OutputStream.WriteAsync(fileBytes);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
                context.Response.StatusCode = 500;
                await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Internal error"));
                context.Response.Close();
            }
        }

        private void CleanupWebView()
        {
            if (_controller != null)
            {
                try
                {
                    _controller.IsVisible = false;
                    _controller.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while closing PDF WebView2: {ex.Message}");
                }
                _controller = null;
                _webview = null;
            }
        }

        private void UpdateBounds()
        {
            if (_controller == null)
                return;

            var root = VisualRoot as Window;
            if (root == null)
                return;

            var scaling = root.RenderScaling;

            var transform = this.TransformToVisual(root);
            var position = transform?.Transform(new Point(0, 0)) ?? new Point(0, 0);

            var width = (int)(Bounds.Width * scaling);
            var height = (int)(Bounds.Height * scaling);
            var x = (int)(position.X * scaling);
            var y = (int)(position.Y * scaling);

            width = Math.Max(100, width);
            height = Math.Max(100, height);

            _controller.Bounds = new System.Drawing.Rectangle(x, y, width, height);
            Console.WriteLine($"PDF WebView2 bounds updated: {_controller.Bounds}");
        }

        public async Task Activate()
        {
            if (_isActive) return;

            _isActive = true;
            Console.WriteLine("PDF viewer activated");

            // 确保控件布局完成后再初始化WebView2
            await WaitForValidBoundsAsync();

            // 确保WebView2已初始化
            if (_controller == null && _pendingFilePath != null)
            {
                await InitializeWebViewAsync();
            }

            if (_controller != null)
            {
                // 更新边界到正确位置
                UpdateBounds();
                
                // 设置可见性
                _controller.IsVisible = true;
            }

            // 重新加载 PDF 内容使用 PDF.js
            if (_pendingFilePath != null && _webview != null)
            {
                await Task.Delay(30); // 减少等待时间
                Console.WriteLine("Activate: Reloading PDF with PDF.js");
                await LoadPdfAsync(_pendingFilePath);
            }
        }
        
        private async Task WaitForValidBoundsAsync()
        {
            int attempts = 0;
            const int maxAttempts = 20;
            
            while (attempts < maxAttempts && (this.Bounds.Width < 100 || this.Bounds.Height < 100))
            {
                await Task.Delay(10);
                attempts++;
            }
            
            Console.WriteLine($"WaitForValidBoundsAsync completed after {attempts} attempts, bounds: {this.Bounds}");
        }

        public async Task DeactivateAsync()
        {
            if (!_isActive) return;

            _isActive = false;
            Console.WriteLine("PDF viewer deactivated");

            // 立即隐藏控制器，防止与其他控件重叠
            if (_controller != null)
            {
                try
                {
                    _controller.IsVisible = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting PDF WebView2 visibility: {ex.Message}");
                }
            }

            // 等待一小段时间确保WebView2完全隐藏
            await Task.Delay(50);

            // 完全关闭WebView2控制器，释放资源
            CleanupWebView();
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
                return;

            _isFullScreen = true;
            FullScreenChanged?.Invoke(this, EventArgs.Empty);
            Console.WriteLine("Entering PDF full screen mode");

            // 创建全屏窗口
            _fullScreenWindow = new Window
            {
                WindowState = WindowState.FullScreen,
                Title = "PDF Full Screen",
                Background = Avalonia.Media.Brushes.Black
            };

            // 添加ESC键处理
            _fullScreenWindow.KeyDown += FullScreenWindow_KeyDown;

            // 添加PDF查看器到全屏窗口
            var fullScreenViewer = new PdfViewerControl();
            _fullScreenWindow.Content = fullScreenViewer;

            // 显示全屏窗口
            _fullScreenWindow.Show();

            // 等待窗口加载完成
            await Task.Delay(100);

            // 加载PDF（确保窗口已初始化）
            await fullScreenViewer.LoadPdfAsync(_pendingFilePath);
            await Task.Delay(100); // 确保WebView2初始化完成
            await fullScreenViewer.Activate();

            // 隐藏当前控件
            if (_controller != null)
            {
                _controller.IsVisible = false;
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
            Console.WriteLine("Exiting PDF full screen mode");

            // 显示当前控件
            if (_controller != null)
            {
                _controller.IsVisible = true;
            }
        }

        private void FullScreenWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitFullScreen();
            }
        }
    }
}
