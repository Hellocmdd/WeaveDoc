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
        
        private static CoreWebView2Environment? _sharedEnvironment;

        public MonacoEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
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

        private async Task InitializeWebViewAsync()
        {
            try
            {
                var root = this.VisualRoot as Window;
                if (root == null)
                {
                    return;
                }

                await Task.Delay(300);

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
                _webview.WebMessageReceived += Webview_WebMessageReceived;
                _webview.NavigationCompleted += Webview_NavigationCompleted;

                _webview.Settings.AreDefaultContextMenusEnabled = false;

                var htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "monaco-editor", "index.html");

                _webview.Navigate(htmlPath);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
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
                        var selectionData = System.Text.Json.JsonDocument.Parse(msgData);
                        var root = selectionData.RootElement;

                        int startLine = 1, startCol = 1, endLine = 1, endCol = 1;
                        if (root.TryGetProperty("startLine", out var startLineProp)) startLine = startLineProp.GetInt32();
                        if (root.TryGetProperty("startColumn", out var startColProp)) startCol = startColProp.GetInt32();
                        if (root.TryGetProperty("endLine", out var endLineProp)) endLine = endLineProp.GetInt32();
                        if (root.TryGetProperty("endColumn", out var endColProp)) endCol = endColProp.GetInt32();

                        var rootWindow = this.VisualRoot as Window;
                        if (rootWindow is MainWindow mainWindow)
                        {
                            mainWindow.ScrollPreviewToSelection(startLine, startCol, endLine, endCol);
                        }
                    }
                    catch (Exception ex)
                    {
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

        public async Task ScrollToPositionAsync(int lineNumber, int column)
        {
            try
            {
                Console.WriteLine($"ScrollToPositionAsync called: line={lineNumber}, column={column}");
                
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null");
                    return;
                }

                // 先检查 Monaco 是否已经初始化
                var editorType = await _webview.ExecuteScriptAsync("typeof editor");
                Console.WriteLine($"editor type: {editorType}");
                
                if (editorType == "\"undefined\"")
                {
                    Console.WriteLine("Editor not yet initialized, scheduling retry");
                    // 延迟重试
                    await Task.Delay(200);
                    await ScrollToPositionAsync(lineNumber, column);
                    return;
                }

                // 直接操作 Monaco Editor API，字符级高亮（高亮2个字符范围）
                var lineCount = lineNumber;
                var script = $@"
                    if (editor) {{
                        console.log('ScrollToPositionAsync: line={lineNumber}, column={column}');

                        var model = editor.getModel();
                        if (model) {{
                            var lineCount = model.getLineCount();
                            var lineLength = model.getLineLength({lineNumber});
                            console.log('Model lineCount=' + lineCount + ', lineLength=' + lineLength);

                            // 验证列号是否有效
                            var validColumn = {column};
                            if (validColumn > lineLength) {{
                                validColumn = lineLength;
                                console.log('Column adjusted to line length: ' + validColumn);
                            }}

                            // 滚动到指定位置
                            editor.revealLine({lineNumber}, 1);
                            editor.setPosition(new monaco.Position({lineNumber}, validColumn));

                            // 清除之前的高亮
                            if (typeof window.highlightDecoration !== 'undefined' && window.highlightDecoration) {{
                                editor.deltaDecorations(window.highlightDecoration, []);
                                window.highlightDecoration = null;
                            }}

                            // 应用新的高亮 - 2个字符范围
                            var highlightRange = new monaco.Range({lineNumber}, validColumn, {lineNumber}, validColumn + 2);
                            console.log('Creating decoration with range: line=' + {lineNumber} + ', startCol=' + validColumn + ', endCol=' + (validColumn + 2));
                            window.highlightDecoration = editor.deltaDecorations([], [{{
                                range: highlightRange,
                                options: {{
                                    isWholeLine: false,
                                    className: 'highlight-char'
                                }}
                            }}]);

                            console.log('Decoration applied, ID:', window.highlightDecoration);
                            'success';
                        }} else {{
                            console.log('Model is null');
                            'model is null';
                        }}
                    }} else {{
                        console.log('Editor is null');
                        'editor is null';
                    }}
                ";
                Console.WriteLine($"Executing direct API call (highlight 2 chars) for line={lineNumber}, column={column}");
                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"Script executed, result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ScrollToPositionAsync: {ex.Message}");
            }
        }

        public async Task ScrollToPositionAsync(int startLine, int startColumn, int endLine, int endColumn)
        {
            await ScrollToPositionAsync(startLine, startColumn, endLine, endColumn, 1);
        }

        public async Task ScrollToPositionAsync(int startLine, int startColumn, int endLine, int endColumn, int selectionLength)
        {
            try
            {
                Console.WriteLine($"ScrollToPositionAsync (range) called: ({startLine},{startColumn}) to ({endLine},{endColumn}), length={selectionLength}");
                
                if (_webview == null)
                {
                    Console.WriteLine("_webview is null");
                    return;
                }

                // 先检查 Monaco 是否已经初始化
                var editorType = await _webview.ExecuteScriptAsync("typeof editor");
                Console.WriteLine($"editor type: {editorType}");
                
                if (editorType == "\"undefined\"")
                {
                    Console.WriteLine("Editor not yet initialized, scheduling retry");
                    await Task.Delay(200);
                    await ScrollToPositionAsync(startLine, startColumn, endLine, endColumn, selectionLength);
                    return;
                }

                // 计算正确的结束列号
                int actualEndColumn = startColumn + selectionLength;
                
                // 直接操作 Monaco Editor API，高亮指定范围
                var script = $@"
                    if (editor) {{
                        console.log('ScrollToPositionAsync (range): ({startLine},{startColumn}) to ({endLine},{actualEndColumn}), length={selectionLength}');

                        var model = editor.getModel();
                        if (model) {{
                            var startLineLen = model.getLineLength({startLine});
                            console.log('Start line length: ' + startLineLen);

                            // 验证列号是否有效
                            var validStartCol = {startColumn};
                            var validEndCol = {actualEndColumn};
                            
                            // 确保起始列有效
                            if (validStartCol < 1) {{
                                validStartCol = 1;
                            }}
                            if (validStartCol > startLineLen) {{
                                validStartCol = startLineLen;
                            }}
                            
                            // 确保结束列有效（不能超过行长度）
                            if (validEndCol > startLineLen + 1) {{
                                validEndCol = startLineLen + 1;
                            }}
                            
                            // 如果结束列小于等于起始列，只高亮一个字符
                            if (validEndCol <= validStartCol) {{
                                validEndCol = validStartCol + 1;
                            }}

                            // 滚动到起始位置
                            editor.revealLine({startLine}, 1);
                            editor.setPosition(new monaco.Position({startLine}, validStartCol));

                            // 清除之前的高亮
                            if (typeof window.highlightDecoration !== 'undefined' && window.highlightDecoration) {{
                                editor.deltaDecorations(window.highlightDecoration, []);
                                window.highlightDecoration = null;
                            }}

                            // 应用新的高亮 - 根据选区长度
                            var highlightRange = new monaco.Range({startLine}, validStartCol, {startLine}, validEndCol);
                            console.log('Creating decoration with range: (' + {startLine} + ',' + validStartCol + ') to (' + {startLine} + ',' + validEndCol + ')');
                            window.highlightDecoration = editor.deltaDecorations([], [{{
                                range: highlightRange,
                                options: {{
                                    isWholeLine: false,
                                    className: 'highlight-char'
                                }}
                            }}]);

                            console.log('Decoration applied, ID:', window.highlightDecoration);
                            'success';
                        }} else {{
                            console.log('Model is null');
                            'model is null';
                        }}
                    }} else {{
                        console.log('Editor is null');
                        'editor is null';
                    }}
                ";
                Console.WriteLine($"Executing direct API call (range highlight) for ({startLine},{startColumn}) to ({startLine},{actualEndColumn})");
                var result = await _webview.ExecuteScriptAsync(script);
                Console.WriteLine($"Script executed, result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ScrollToPositionAsync (range): {ex.Message}");
            }
        }
    }
}
