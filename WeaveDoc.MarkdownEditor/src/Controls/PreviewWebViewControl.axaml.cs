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

                var htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "preview-template.html");
                var html = File.ReadAllText(htmlPath);
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

                if (msgType == "previewClick" && msgData != null)
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
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void UpdatePreview(string content)
        {
            Console.WriteLine($"UpdatePreview called, content length: {content.Length}");
            try
            {
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null, setting pending content");
                    _pendingContent = content;
                    return;
                }

                if (!_isInitialized)
                {
                    Console.WriteLine("_isInitialized is false, setting pending content");
                    _pendingContent = content;
                    return;
                }

                var script = $"window.updateContent({System.Text.Json.JsonSerializer.Serialize(content)});";
                Console.WriteLine($"Executing script: {script.Substring(0, Math.Min(100, script.Length))}...");

                await _webview.ExecuteScriptAsync(script);
                Console.WriteLine("Script executed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdatePreview exception: {ex.Message}");
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
                if (_webview == null || !_isInitialized)
                {
                    return;
                }

                var script = $"window.scrollToSelection({startLine}, {startCol}, {endLine}, {endCol});";
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
