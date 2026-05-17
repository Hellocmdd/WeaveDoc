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
        private string? _pendingFilePath;
        private bool _isActive;

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
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async Task LoadPdfAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            _pendingFilePath = filePath;

            // 清理旧的WebView2（如果存在）
            CleanupWebView();

            try
            {
                var root = VisualRoot as Window;
                if (root == null)
                    return;

                await Task.Delay(50);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                var environment = await CoreWebView2Environment.CreateAsync(null, null, null);
                _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);

                // 设置WebView2的位置和大小
                UpdateBounds();
                
                // 导航到PDF
                string fileUri = "file:///" + filePath.Replace("\\", "/");
                Console.WriteLine($"Navigating to PDF: {fileUri}");
                _controller.CoreWebView2.Navigate(fileUri);

                _controller.IsVisible = true;
                Console.WriteLine("WebView2 initialized and PDF loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize WebView2: {ex.Message}");
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
                    Console.WriteLine($"Error while closing WebView2: {ex.Message}");
                }
                _controller = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty && _isActive)
            {
                UpdateBounds();
            }
        }

        private void UpdateBounds()
        {
            if (_controller == null || !_isActive)
                return;

            var scaling = VisualRoot is Window window ? window.RenderScaling : 1.0;
            
            var topOffset = 80;
            var x = Bounds.X;
            var y = Bounds.Y + topOffset;
            var width = Math.Max(1, Bounds.Width);
            var height = Math.Max(1, Bounds.Height - topOffset);

            _controller.Bounds = new System.Drawing.Rectangle(
                (int)(x * scaling),
                (int)(y * scaling),
                (int)(width * scaling),
                (int)(height * scaling)
            );
            Console.WriteLine($"WebView2 bounds updated: {_controller.Bounds}");
        }

        public async Task Activate()
        {
            _isActive = true;
            Console.WriteLine("PDF viewer activated");

            if (_pendingFilePath != null)
            {
                await LoadPdfAsync(_pendingFilePath);
            }
        }

        public void Deactivate()
        {
            _isActive = false;
            Console.WriteLine("PDF viewer deactivated");
            CleanupWebView();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _ = Activate();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            Deactivate();
        }
    }
}