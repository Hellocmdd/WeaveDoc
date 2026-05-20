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
                // 使用HTTP服务器加载PDF.js和PDF文件
                string viewerUrl = $"http://localhost:{_serverPort}/pdfjs-5.7.284-dist/web/viewer.html?file=http://localhost:{_serverPort}/pdf/{Uri.EscapeDataString(filePath)}";
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

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var root = VisualRoot as Window;
                if (root == null)
                    return;

                await Task.Delay(50);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                // 启动本地HTTP服务器
                StartHttpServer();

                if (_sharedEnvironment == null)
                {
                    _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = "--disable-gpu --disable-software-rasterizer --disable-dev-shm-usage --no-sandbox",
                        AllowSingleSignOnUsingOSPrimaryAccount = false
                    });
                }

                _controller = await _sharedEnvironment.CreateCoreWebView2ControllerAsync(hwnd);
                _webview = _controller.CoreWebView2;

                // 启用右键菜单
                _webview.Settings.AreDefaultContextMenusEnabled = true;

                // 初始化时始终设置为不可见，由Activate方法控制显示时机
                _controller.IsVisible = false;
                
                // 设置一个初始的小尺寸和远离可见区域的位置，防止第一次初始化时意外显示
                _controller.Bounds = new System.Drawing.Rectangle(-1000, -1000, 100, 100);
            }
            catch { }
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
                        catch { }
                    }
                });
            }
            catch { }
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

                if (requestPath.StartsWith("/pdf/"))
                {
                    string pdfPath = requestPath.Substring(5);
                    filePath = Uri.UnescapeDataString(pdfPath);
                }
                else
                {
                    filePath = Path.Combine(assetsDir, requestPath.TrimStart('/'));
                }

                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Not found"));
                    context.Response.Close();
                    return;
                }

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
                await context.Response.OutputStream.WriteAsync(fileBytes);
                context.Response.Close();
            }
            catch
            {
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
                catch { }
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

            // 重新加载PDF内容使用PDF.js
            if (_pendingFilePath != null && _webview != null)
            {
                await Task.Delay(100); // 等待WebView2准备就绪
                Console.WriteLine("Activate: Reloading PDF with PDF.js");
                await LoadPdfAsync(_pendingFilePath);
            }
        }
        
        private async Task WaitForValidBoundsAsync()
        {
            int attempts = 0;
            const int maxAttempts = 50;
            
            while (attempts < maxAttempts && (this.Bounds.Width < 100 || this.Bounds.Height < 100))
            {
                await Task.Delay(20);
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