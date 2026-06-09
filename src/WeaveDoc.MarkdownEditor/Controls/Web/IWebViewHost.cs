using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

public interface IWebViewHost : IAsyncDisposable
{
    Control View { get; }

    bool IsAvailable { get; }

    string? UnavailableReason { get; }

    string AdapterDescription { get; }

    event EventHandler<WebViewHostNavigationCompletedEventArgs>? NavigationCompleted;

    event EventHandler<WebViewHostMessageReceivedEventArgs>? MessageReceived;

    void Navigate(Uri source);

    void NavigateToString(string html, Uri baseUri);

    Task<string> InvokeScriptAsync(string script);

    Task PostJsonAsync(string json);

    Task PostStringAsync(string message);

    void Focus();
}

public sealed class WebViewHostNavigationCompletedEventArgs : EventArgs
{
    public WebViewHostNavigationCompletedEventArgs(bool isSuccess)
    {
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }
}

public sealed class WebViewHostMessageReceivedEventArgs : EventArgs
{
    public WebViewHostMessageReceivedEventArgs(string body)
    {
        Body = body;
    }

    public string Body { get; }
}
