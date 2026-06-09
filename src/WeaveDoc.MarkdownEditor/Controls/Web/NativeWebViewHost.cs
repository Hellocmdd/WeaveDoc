using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

public sealed class NativeWebViewHost : IWebViewHost
{
    private readonly NativeWebView _webView;
    private bool _disposed;

    public NativeWebViewHost()
    {
        _webView = new NativeWebView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.White
        };

        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.AdapterCreated += OnAdapterCreated;
        _webView.AdapterDestroyed += OnAdapterDestroyed;
        _webView.NavigationCompleted += OnNavigationCompleted;
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    public Control View => _webView;

    public bool IsAvailable { get; private set; } = true;

    public string? UnavailableReason { get; private set; }

    public string AdapterDescription { get; private set; } = "NativeWebView adapter not initialized";

    public event EventHandler<WebViewHostNavigationCompletedEventArgs>? NavigationCompleted;

    public event EventHandler<WebViewHostMessageReceivedEventArgs>? MessageReceived;

    public void Navigate(Uri source)
    {
        try
        {
            _webView.Navigate(source);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            throw;
        }
    }

    public void NavigateToString(string html, Uri baseUri)
    {
        try
        {
            _webView.NavigateToString(html, baseUri);
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            throw;
        }
    }

    public async Task<string> InvokeScriptAsync(string script)
    {
        try
        {
            return await _webView.InvokeScript(script).ConfigureAwait(true) ?? string.Empty;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex);
            throw;
        }
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
        _webView.Focus();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _webView.EnvironmentRequested -= OnEnvironmentRequested;
        _webView.AdapterCreated -= OnAdapterCreated;
        _webView.AdapterDestroyed -= OnAdapterDestroyed;
        _webView.NavigationCompleted -= OnNavigationCompleted;
        _webView.WebMessageReceived -= OnWebMessageReceived;
        return ValueTask.CompletedTask;
    }

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
        e.EnableDevTools = false;

        if (e is LinuxWpeWebViewEnvironmentRequestedEventArgs linuxWpe)
        {
            linuxWpe.PreferWebKitGtkInstead = false;
            Logger.Log("NativeWebViewHost environment requested: Linux WPE preferred over WebKitGTK.");
        }
        else if (e is GtkWebViewEnvironmentRequestedEventArgs gtk)
        {
            gtk.ExperimentalOffscreen = true;
            Logger.Log("NativeWebViewHost environment requested: GTK experimental offscreen enabled.");
        }
    }

    private void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        IsAvailable = true;
        UnavailableReason = null;
        AdapterDescription = _webView.AdapterInfo?.ToString() ?? "NativeWebView adapter initialized";
        Logger.Log($"NativeWebViewHost adapter created: {AdapterDescription}");
    }

    private void OnAdapterDestroyed(object? sender, WebViewAdapterEventArgs e)
    {
        AdapterDescription = "NativeWebView adapter destroyed";
        Logger.Log("NativeWebViewHost adapter destroyed.");
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        NavigationCompleted?.Invoke(this, new WebViewHostNavigationCompletedEventArgs(e.IsSuccess));
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, new WebViewHostMessageReceivedEventArgs(e.Body ?? string.Empty));
    }

    private void MarkUnavailable(Exception ex)
    {
        IsAvailable = false;
        UnavailableReason = $"跨平台 WebView 不可用：{ex.Message}";
    }
}
