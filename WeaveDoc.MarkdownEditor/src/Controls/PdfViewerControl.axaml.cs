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
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private bool _isInitialized = false;
        private string _pendingFilePath = string.Empty;

        public static readonly StyledProperty<string> PdfFilePathProperty =
            AvaloniaProperty.Register<PdfViewerControl, string>(nameof(PdfFilePath));

        public string PdfFilePath
        {
            get => GetValue(PdfFilePathProperty);
            set => SetValue(PdfFilePathProperty, value);
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
            _ = InitializeWebViewAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            if (_controller != null)
            {
                _controller.Close();
                _controller = null;
            }
            _webview = null;
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
                    return;

                await Task.Delay(500);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                var environment = await CoreWebView2Environment.CreateAsync(null, null, new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-gpu --disable-software-rasterizer --disable-dev-shm-usage --no-sandbox",
                    AllowSingleSignOnUsingOSPrimaryAccount = false
                });

                _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);
                _controller.IsVisible = true;
                _controller.CoreWebView2.Settings.IsScriptEnabled = true;

                _webview = _controller.CoreWebView2;
                _webview.NavigationCompleted += OnNavigationCompleted;

                _webview.Settings.AreDefaultContextMenusEnabled = true;
                _webview.Settings.IsZoomControlEnabled = true;

                UpdateControllerBounds();

                var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "pdf-viewer-template.html");
                string htmlContent;

                if (File.Exists(templatePath))
                {
                    htmlContent = File.ReadAllText(templatePath);
                }
                else
                {
                    htmlContent = GetDefaultPdfViewerHtml();
                }

                _webview.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize WebView: {ex.Message}");
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess && !_isInitialized)
            {
                _isInitialized = true;

                if (!string.IsNullOrEmpty(_pendingFilePath))
                {
                    _ = LoadPdfAsync(_pendingFilePath);
                    _pendingFilePath = string.Empty;
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PdfFilePathProperty && !string.IsNullOrEmpty(PdfFilePath))
            {
                _ = LoadPdfAsync(PdfFilePath);
            }
        }

        public async Task LoadPdfAsync(string filePath)
        {
            if (_webview == null)
            {
                _pendingFilePath = filePath;
                return;
            }

            if (!File.Exists(filePath))
                return;

            PdfFilePath = filePath;

            await Task.Delay(300);

            // 使用 file:// 协议处理本地文件路径
            var fileUri = new Uri(filePath).AbsoluteUri;
            // 确保 URI 正确处理 Windows 路径
            if (!fileUri.StartsWith("file:///"))
            {
                fileUri = "file:///" + filePath.Replace("\\", "/");
            }

            try
            {
                await _webview.ExecuteScriptAsync($"window.loadPdf('{fileUri}')");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load PDF: {ex.Message}");
            }
        }

        private void UpdateControllerBounds()
        {
            if (_controller != null)
            {
                var scaling = 1.0;
                var visualRoot = VisualRoot as Window;
                if (visualRoot != null)
                {
                    scaling = visualRoot.RenderScaling;
                }

                _controller.Bounds = new System.Drawing.Rectangle(
                    0,
                    0,
                    (int)(Bounds.Width * scaling),
                    (int)(Bounds.Height * scaling)
                );
            }
        }

        private string GetDefaultPdfViewerHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        html,body{margin:0;padding:0;width:100%;height:100%;background:#f5f5f5;display:flex;justify-content:center;align-items:center;}
        canvas{max-width:100%;max-height:100%;}
    </style>
</head>
<body>
    <canvas id='pdfCanvas'></canvas>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.5.136/pdf.min.js'></script>
    <script>
        var pdfDoc=null,canvas=document.getElementById('pdfCanvas'),ctx=canvas.getContext('2d');
        pdfjsLib.GlobalWorkerOptions.workerSrc='https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.5.136/pdf.worker.min.js';
        window.loadPdf=async function(f){var r=await fetch(f),b=await r.arrayBuffer();pdfDoc=await pdfjsLib.getDocument(b).promise;var p=await pdfDoc.getPage(1),v=p.getViewport({scale:1.5});canvas.width=v.width;canvas.height=v.height;await p.render({canvasContext:ctx,viewport:v}).promise;};
    </script>
</body>
</html>";
        }
    }
}