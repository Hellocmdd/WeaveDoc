using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;

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

        private void OnUnloaded(object? sender, EventArgs e)
        {
            Deactivate();
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

            if (_webview != null)
            {
                // 直接导航到PDF，使用URL参数禁用工具栏
                string fileUri = "file:///" + filePath.Replace("\\", "/");
                // 添加参数禁用工具栏和导航面板
                string pdfUrlWithParams = fileUri + "#toolbar=0&navpanes=0&scrollbar=1&view=FitH";
                Console.WriteLine($"Navigating to PDF: {pdfUrlWithParams}");
                _webview.Navigate(pdfUrlWithParams);
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

                // 禁用右键菜单
                _webview.Settings.AreDefaultContextMenusEnabled = false;

                _controller.IsVisible = false;
                UpdateBounds();

                Console.WriteLine("PDF WebView2 initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize PDF WebView2: {ex.Message}");
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

            if (_controller != null)
            {
                UpdateBounds();
                _controller.IsVisible = true;
            }

            if (_pendingFilePath != null && _webview == null)
            {
                await LoadPdfAsync(_pendingFilePath);
            }
        }

        public void Deactivate()
        {
            if (!_isActive) return;

            _isActive = false;
            Console.WriteLine("PDF viewer deactivated");

            if (_controller != null)
            {
                _controller.IsVisible = false;
            }
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

            // 加载PDF
            await fullScreenViewer.LoadPdfAsync(_pendingFilePath);
            await Task.Yield(); // 确保异步操作完成
            fullScreenViewer.Activate();

            // 显示全屏窗口
            _fullScreenWindow.Show();

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