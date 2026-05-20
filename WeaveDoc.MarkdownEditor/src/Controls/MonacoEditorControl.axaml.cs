using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Web.WebView2.Core;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class MonacoEditorControl : UserControl
    {
        private CoreWebView2? _webview;
        private CoreWebView2Controller? _controller;
        private string _pendingContent = string.Empty;
        private bool _isInitializing = false;
        private bool _isActive = true;
        
        private static CoreWebView2Environment? _sharedEnvironment;

        public MonacoEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            PointerPressed += OnPointerPressed;
            GotFocus += OnGotFocus;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (!_isInitializing)
            {
                _isInitializing = true;
                await InitializeWebViewAsync();
            }
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            // 只隐藏，不关闭，以保留内容
            if (_controller != null)
            {
                _controller.IsVisible = false;
            }
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

                await Task.Delay(800);

                var hwnd = root.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                await WaitForValidBounds();

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
                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                _webview.Settings.AreDefaultContextMenusEnabled = false;

                // 延迟设置边界，确保布局完成
                await Task.Delay(300);
                UpdateControllerBounds(true);

                var htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html");

                _webview.Navigate(htmlPath);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async Task WaitForValidBounds()
        {
            int attempts = 0;
            while (attempts < 100 && (this.Bounds.Width < 100 || this.Bounds.Height < 100))
            {
                await Task.Delay(20);
                attempts++;
            }
        }

        private void UpdateControllerBounds(bool force = false)
        {
            try
            {
                if (_controller == null) return;

                var root = this.VisualRoot as Window;
                if (root == null) return;

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

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateControllerBounds();

            if (_webview != null)
            {
                _ = ResizeMonacoEditorAsync((int)(e.NewSize.Width * 2), (int)(e.NewSize.Height * 2));
            }
        }

        private async Task ResizeMonacoEditorAsync(int width, int height)
        {
            try
            {
                if (_webview == null) return;

                var script = $"window.resizeEditor({width}, {height});";
                await _webview.ExecuteScriptAsync(script);
            }
            catch
            {
            }
        }

        private void Webview_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var json = args.WebMessageAsJson;
                
                if (string.IsNullOrWhiteSpace(json)) 
                {
                    return;
                }

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

                if (msgType == "contentChanged" && msgData != null)
                {
                    MainWindowViewModel? vm = null;

                    if (DataContext is MainWindowViewModel)
                    {
                        vm = DataContext as MainWindowViewModel;
                    }
                    else
                    {
                        var visualRoot = this.VisualRoot;
                        if (visualRoot is Window window && window.DataContext is MainWindowViewModel)
                        {
                            vm = window.DataContext as MainWindowViewModel;
                        }
                    }

                    if (vm != null)
                    {
                        vm.EditorContent = msgData;
                    }
                }
                else if (msgType == "selectionChanged" && msgData != null)
                {
                    try
                    {
                        Console.WriteLine($"MonacoEditorControl: Received selectionChanged message: {msgData}");
                        
                        var selectionData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = selectionData.RootElement;

                        int startLine = 1, startCol = 1, endLine = 1, endCol = 1;
                        if (root.TryGetProperty("startLine", out var startLineProp)) startLine = startLineProp.GetInt32();
                        if (root.TryGetProperty("startColumn", out var startColProp)) startCol = startColProp.GetInt32();
                        if (root.TryGetProperty("endLine", out var endLineProp)) endLine = endLineProp.GetInt32();
                        if (root.TryGetProperty("endColumn", out var endColProp)) endCol = endColProp.GetInt32();

                        Console.WriteLine($"MonacoEditorControl: Selection - startLine={startLine}, startCol={startCol}, endLine={endLine}, endCol={endCol}");

                        var rootWindow = this.VisualRoot as Window;
                        if (rootWindow is MainWindow mainWindow)
                        {
                            Console.WriteLine("MonacoEditorControl: Calling mainWindow.ScrollPreviewToSelection");
                            mainWindow.ScrollPreviewToSelection(startLine, startCol, endLine, endCol);
                        }
                        else
                        {
                            Console.WriteLine($"MonacoEditorControl: rootWindow is null or not MainWindow: {rootWindow}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MonacoEditorControl: selectionChanged exception: {ex.Message}");
                        Logger.LogException(ex);
                    }
                }
                
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private async void Webview_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                Console.WriteLine("Monaco Editor NavigationCompleted");
                await Task.Delay(200);

                UpdateControllerBounds(true);

                await Task.Delay(100);

                if (_webview != null && _controller != null)
                {
                    var root = VisualRoot as Window;
                    if (root != null)
                    {
                        var scaling = root.RenderScaling;
                        var width = (int)(this.Bounds.Width * scaling * 2);
                        var height = (int)(this.Bounds.Height * scaling * 2);

                        await ResizeMonacoEditorAsync(width, height);
                    }
                }

                if (!string.IsNullOrEmpty(_pendingContent))
                {
                    SetContentAsync(_pendingContent);
                    _pendingContent = string.Empty;
                }
                
                // 等待 Monaco 编辑器完全初始化
                await WaitForMonacoReadyAsync();
            }
        }
        
        private async Task WaitForMonacoReadyAsync()
        {
            int attempts = 0;
            const int maxAttempts = 30;
            
            while (attempts < maxAttempts)
            {
                try
                {
                    if (_webview == null)
                    {
                        Console.WriteLine("_webview is null in WaitForMonacoReadyAsync");
                        await Task.Delay(100);
                        attempts++;
                        continue;
                    }
                    
                    // 只检查 editor 对象是否存在（scrollToPosition 在 require 回调内部定义，不在全局作用域）
                    var editorResult = await _webview.ExecuteScriptAsync("typeof editor");
                    
                    Console.WriteLine($"Attempt {attempts + 1}: editor={editorResult}");
                    
                    if (editorResult == "\"object\"")
                    {
                        var mainWindow = VisualRoot as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.SetMonacoReady(true);
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking Monaco ready state: {ex.Message}");
                }
                
                attempts++;
                await Task.Delay(200);
            }
            
            Console.WriteLine("Monaco Editor failed to initialize within timeout");
        }

        public void SetContentAsync(string content)
        {
            try
            {
                if (_webview == null)
                {
                    _pendingContent = content;
                    return;
                }

                var obj = new { Type = "setContent", Data = content };
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                _webview.PostWebMessageAsJson(json);
            }
            catch
            {
            }
        }

        public async Task ScrollToLineAsync(int lineNumber)
        {
            try
            {
                if (_webview == null)
                {
                    return;
                }

                var script = $"window.scrollToLine({lineNumber});";
                await _webview.ExecuteScriptAsync(script);
            }
            catch
            {
            }
        }

        public async Task ScrollToPositionAsync(int lineNumber, int column, int selectionLength = 1)
        {
            try
            {
                Console.WriteLine($"ScrollToPositionAsync called: line={lineNumber}, column={column}, length={selectionLength}");
                
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null");
                    return;
                }

                var editorType = await _webview.ExecuteScriptAsync("typeof editor");
                Console.WriteLine($"editor type: {editorType}");
                
                if (editorType == "\"undefined\"")
                {
                    Console.WriteLine("Editor not yet initialized, scheduling retry");
                    await Task.Delay(200);
                    await ScrollToPositionAsync(lineNumber, column, selectionLength);
                    return;
                }

                int endColumn = column + selectionLength;
                
                var script = $@"
                    if (editor) {{
                        console.log('Revealing line and setting position: line={lineNumber}, col={column}, endCol={endColumn}');
                        
                        if (typeof window.highlightDecoration === 'undefined') {{
                            window.highlightDecoration = null;
                        }}
                        
                        if (window.highlightDecoration) {{
                            editor.deltaDecorations(window.highlightDecoration, []);
                            window.highlightDecoration = null;
                        }}
                        
                        // 滚动到指定行
                        editor.revealLine({lineNumber}, 1);
                        
                        // 设置光标位置
                        editor.setPosition(new monaco.Position({lineNumber}, {column}));
                        
                        var range = new monaco.Range({lineNumber}, {column}, {lineNumber}, {endColumn});
                        console.log('Creating decoration with range: line={lineNumber}, startCol={column}, endCol={endColumn}');
                        
                        window.highlightDecoration = editor.deltaDecorations([], [{{
                            range: range,
                            options: {{
                                isWholeLine: false,
                                className: 'highlight-char'
                            }}
                        }}]);
                        
                        console.log('Decoration applied, ID:', window.highlightDecoration);
                        'success';
                    }} else {{
                        'editor is null';
                    }}
                ";
                Console.WriteLine($"Executing highlight script for line={lineNumber}, col={column}, endCol={endColumn}");
                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"Script executed, result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ScrollToPositionAsync: {ex.Message}");
            }
        }

        public async Task ClearHighlightAsync()
        {
            try
            {
                Console.WriteLine("ClearHighlightAsync called");
                
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null");
                    return;
                }

                var script = @"
                    if (editor && window.highlightDecoration) {
                        editor.deltaDecorations(window.highlightDecoration, []);
                        window.highlightDecoration = null;
                        console.log('Highlight cleared');
                        'success';
                    } else {
                        'no highlight to clear';
                    }
                ";
                
                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"ClearHighlightAsync executed, result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ClearHighlightAsync: {ex.Message}");
            }
        }

        private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            Console.WriteLine("MonacoEditorControl: Pointer pressed, clearing highlight");
            _ = ClearHighlightAsync();
        }

        private void OnGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
        {
            Console.WriteLine("MonacoEditorControl: Got focus, clearing highlight");
            _ = ClearHighlightAsync();
        }

        public async Task RequestCurrentSelectionAsync()
        {
            try
            {
                Console.WriteLine("RequestCurrentSelectionAsync called");

                if (_webview == null)
                {
                    Console.WriteLine($"RequestCurrentSelectionAsync: webview is null");
                    return;
                }

                var script = @"
                    (function() {
                        if (editor) {
                            var selection = editor.getSelection();
                            if (selection) {
                                var msg = {
                                    type: 'selectionChanged',
                                    data: JSON.stringify({
                                        startLine: selection.startLineNumber,
                                        startColumn: selection.startColumn,
                                        endLine: selection.endLineNumber,
                                        endColumn: selection.endColumn
                                    })
                                };
                                window.chrome.webview.postMessage(JSON.stringify(msg));
                                return 'selection sent';
                            }
                        }
                        return 'no selection';
                    })();
                ";

                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"RequestCurrentSelectionAsync result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RequestCurrentSelectionAsync error: {ex.Message}");
            }
        }
        
        public async Task Activate(bool forceReset = false)
        {
            if (_isActive) return;
            
            _isActive = true;
            Console.WriteLine("MonacoEditorControl: Activating...");
            
            if (_controller != null)
            {
                _controller.IsVisible = true;
                await Task.Delay(200); // 等待WebView完全就绪
                UpdateControllerBounds(true);
                
                // 确保Monaco编辑器接收焦点以便发送选择事件
                try
                {
                    _controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                    
                    // 通过JavaScript确保Monaco编辑器获得焦点
                    if (_webview != null)
                    {
                        await _webview.ExecuteScriptAsync("if (editor) { editor.focus(); console.log('Monaco editor focus called'); }");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MoveFocus exception: {ex.Message}");
                }
                
                // 只有在强制重置或有待处理内容时才设置内容
                if (forceReset && !string.IsNullOrEmpty(_pendingContent))
                {
                    Console.WriteLine("MonacoEditorControl: Setting pending content on activate (force reset)");
                    SetContentAsync(_pendingContent);
                }
            }
            else
            {
                // 如果控制器不存在，重新初始化
                _isInitializing = false;
                await InitializeWebViewAsync();
            }
            
            var mainWindow = VisualRoot as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SetMonacoReady(true);
            }
        }
        
        public void Deactivate()
        {
            if (!_isActive) return;
            
            _isActive = false;
            Console.WriteLine("MonacoEditorControl: Deactivating...");
            
            if (_controller != null)
            {
                _controller.IsVisible = false;
            }
            
            var mainWindow = VisualRoot as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SetMonacoReady(false);
            }
        }
    }
}
