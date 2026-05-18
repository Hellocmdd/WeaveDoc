using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
                string fileUri = "file:///" + filePath.Replace("\\", "/");
                Console.WriteLine($"Navigating to PDF: {fileUri}");
                _webview.Navigate(fileUri);
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
    }
}