using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Web.WebView2.Core;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

public sealed class WindowsWebView2Host : IWebViewHost
{
    private static readonly object EnvironmentLock = new();
    private static Task<CoreWebView2Environment>? _sharedEnvironmentTask;

    private readonly Border _view;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _webView;
    private TaskCompletionSource<bool>? _initializationCompletion;
    private bool _isInitializing;
    private bool _disposed;
    private bool _isVisible = true;

    public WindowsWebView2Host()
    {
        _view = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };

        _view.AttachedToVisualTree += View_AttachedToVisualTree;
        _view.DetachedFromVisualTree += View_DetachedFromVisualTree;
        _view.LayoutUpdated += View_LayoutUpdated;
        _view.PropertyChanged += View_PropertyChanged;
    }

    public Control View => _view;

    public bool IsAvailable { get; private set; } = true;

    public string? UnavailableReason { get; private set; }

    public string AdapterDescription { get; private set; } = "Windows WebView2 adapter not initialized";

    public event EventHandler<WebViewHostNavigationCompletedEventArgs>? NavigationCompleted;

    public event EventHandler<WebViewHostMessageReceivedEventArgs>? MessageReceived;

    public void Navigate(Uri source)
    {
        _ = RunWhenInitializedAsync(webView => webView.Navigate(source.AbsoluteUri));
    }

    public void NavigateToString(string html, Uri baseUri)
    {
        var htmlWithBaseUri = InjectBaseUri(html, baseUri);
        _ = RunWhenInitializedAsync(webView => webView.NavigateToString(htmlWithBaseUri));
    }

    public async Task<string> InvokeScriptAsync(string script)
    {
        var webView = await EnsureInitializedAsync().ConfigureAwait(true);
        return await webView.ExecuteScriptAsync(script).ConfigureAwait(true) ?? string.Empty;
    }

    public Task PostJsonAsync(string json)
    {
        return InvokeScriptAsync(WebViewBridge.BuildReceiveScript(json));
    }

    public Task PostStringAsync(string message)
    {
        return InvokeScriptAsync(WebViewBridge.BuildReceiveStringScript(message));
    }

    public void Focus()
    {
        _view.Focus();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _view.AttachedToVisualTree -= View_AttachedToVisualTree;
        _view.DetachedFromVisualTree -= View_DetachedFromVisualTree;
        _view.LayoutUpdated -= View_LayoutUpdated;
        _view.PropertyChanged -= View_PropertyChanged;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_webView != null)
            {
                _webView.NavigationCompleted -= WebView_NavigationCompleted;
                _webView.WebMessageReceived -= WebView_WebMessageReceived;
            }

            try
            {
                _controller?.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"WindowsWebView2Host close failed: {ex.Message}");
            }

            _controller = null;
            _webView = null;
        });
    }

    private void View_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = EnsureInitializedAsync();
    }

    private void View_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SetControllerVisible(false);
    }

    private void View_LayoutUpdated(object? sender, EventArgs e)
    {
        UpdateBounds();
    }

    private void View_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            _isVisible = _view.IsVisible;
            SetControllerVisible(_isVisible);
        }
    }

    private async Task RunWhenInitializedAsync(Action<CoreWebView2> action)
    {
        try
        {
            var webView = await EnsureInitializedAsync().ConfigureAwait(true);
            action(webView);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            Logger.LogException(ex);
            throw;
        }
    }

    private Task<CoreWebView2> EnsureInitializedAsync()
    {
        if (_webView != null)
        {
            return Task.FromResult(_webView);
        }

        if (_disposed)
        {
            return Task.FromException<CoreWebView2>(
                new ObjectDisposedException(nameof(WindowsWebView2Host)));
        }

        _initializationCompletion ??= new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_isInitializing)
        {
            _isInitializing = true;
            _ = InitializeOnUiThreadAsync();
        }

        return WaitForInitializedWebViewAsync(_initializationCompletion.Task);
    }

    private async Task<CoreWebView2> WaitForInitializedWebViewAsync(Task initializationTask)
    {
        await initializationTask.ConfigureAwait(true);
        return _webView ?? throw new InvalidOperationException("Windows WebView2 初始化失败。");
    }

    private async Task InitializeOnUiThreadAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = await WaitForWindowAsync().ConfigureAwait(true);
                var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Windows WebView2 初始化失败：无法获取窗口句柄。");
                }

                var environment = await GetSharedEnvironmentAsync().ConfigureAwait(true);
                _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd).ConfigureAwait(true);
                _webView = _controller.CoreWebView2;
                _webView.NavigationCompleted += WebView_NavigationCompleted;
                _webView.WebMessageReceived += WebView_WebMessageReceived;
                _webView.Settings.IsScriptEnabled = true;
                _webView.Settings.AreDefaultContextMenusEnabled = true;
                _webView.Settings.IsZoomControlEnabled = true;

                await _webView.AddScriptToExecuteOnDocumentCreatedAsync(WebViewBridge.Script).ConfigureAwait(true);

                AdapterDescription = "Windows WebView2 adapter initialized";
                IsAvailable = true;
                UnavailableReason = null;
                UpdateBounds();
                SetControllerVisible(_view.IsVisible && _isVisible);
                _initializationCompletion?.TrySetResult(true);
            }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            Logger.LogException(ex);
            _initializationCompletion?.TrySetException(ex);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task<Window> WaitForWindowAsync()
    {
        const int maxAttempts = 80;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WindowsWebView2Host));
            }

            var topLevel = TopLevel.GetTopLevel(_view);
            if (topLevel is Window window
                && window.TryGetPlatformHandle()?.Handle is { } hwnd
                && hwnd != IntPtr.Zero)
            {
                return window;
            }

            await Task.Delay(25).ConfigureAwait(true);
        }

        throw new InvalidOperationException("Windows WebView2 初始化失败：控件尚未挂载到窗口。");
    }

    private static Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        lock (EnvironmentLock)
        {
            _sharedEnvironmentTask ??= CreateEnvironmentAsync();
            return _sharedEnvironmentTask;
        }
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WeaveDoc",
            "WebView2");
        Directory.CreateDirectory(userDataFolder);
        return CoreWebView2Environment.CreateAsync(null, userDataFolder);
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        NavigationCompleted?.Invoke(this, new WebViewHostNavigationCompletedEventArgs(e.IsSuccess));
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, new WebViewHostMessageReceivedEventArgs(e.WebMessageAsJson ?? string.Empty));
    }

    private void UpdateBounds()
    {
        if (_controller == null || _view.Bounds.Width <= 0 || _view.Bounds.Height <= 0)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel == null)
        {
            return;
        }

        try
        {
            var transform = _view.TransformToVisual(topLevel);
            var position = transform?.Transform(new Point(0, 0)) ?? default;
            var scaling = topLevel.RenderScaling;
            var x = (int)Math.Round(position.X * scaling);
            var y = (int)Math.Round(position.Y * scaling);
            var width = Math.Max(1, (int)Math.Round(_view.Bounds.Width * scaling));
            var height = Math.Max(1, (int)Math.Round(_view.Bounds.Height * scaling));

            _controller.Bounds = new System.Drawing.Rectangle(x, y, width, height);
        }
        catch (Exception ex)
        {
            Logger.Log($"WindowsWebView2Host bounds update failed: {ex.Message}");
        }
    }

    private void SetControllerVisible(bool visible)
    {
        try
        {
            if (_controller != null)
            {
                _controller.IsVisible = visible && !_disposed;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"WindowsWebView2Host visibility update failed: {ex.Message}");
        }
    }

    private void MarkUnavailable(Exception ex)
    {
        IsAvailable = false;
        UnavailableReason = $"Windows WebView2 不可用：{ex.Message}";
    }

    private static string InjectBaseUri(string html, Uri baseUri)
    {
        if (string.IsNullOrEmpty(html) || !baseUri.IsAbsoluteUri)
        {
            return html;
        }

        var baseElement = $"<base href=\"{baseUri.AbsoluteUri}\">";
        var headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (headIndex < 0)
        {
            return baseElement + html;
        }

        var headEndIndex = html.IndexOf('>', headIndex);
        if (headEndIndex < 0)
        {
            return baseElement + html;
        }

        return html.Insert(headEndIndex + 1, baseElement);
    }
}
