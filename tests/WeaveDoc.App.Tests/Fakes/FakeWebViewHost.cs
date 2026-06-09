using Avalonia.Controls;
using WeaveDoc.MarkdownEditor.Controls.Web;

namespace WeaveDoc.App.Tests.Fakes;

public sealed class FakeWebViewHostFactory : IWebViewHostFactory
{
    public List<FakeWebViewHost> Hosts { get; } = [];

    public IWebViewHost Create()
    {
        var host = new FakeWebViewHost();
        Hosts.Add(host);
        return host;
    }
}

public sealed class FakeWebViewHost : IWebViewHost
{
    public Border View { get; } = new()
    {
        Name = "FakeWebViewHost"
    };

    Control IWebViewHost.View => View;

    public bool IsAvailable { get; set; } = true;

    public string? UnavailableReason { get; set; }

    public string AdapterDescription { get; set; } = "Fake WebView host";

    public List<Uri> NavigatedUris { get; } = [];

    public List<string> NavigatedHtml { get; } = [];

    public List<string> InvokedScripts { get; } = [];

    public List<string> PostedJson { get; } = [];

    public event EventHandler<WebViewHostNavigationCompletedEventArgs>? NavigationCompleted;

    public event EventHandler<WebViewHostMessageReceivedEventArgs>? MessageReceived;

    public void Navigate(Uri source)
    {
        NavigatedUris.Add(source);
        NavigationCompleted?.Invoke(this, new WebViewHostNavigationCompletedEventArgs(true));
    }

    public void NavigateToString(string html, Uri baseUri)
    {
        NavigatedHtml.Add(html);
        NavigationCompleted?.Invoke(this, new WebViewHostNavigationCompletedEventArgs(true));
    }

    public Task<string> InvokeScriptAsync(string script)
    {
        InvokedScripts.Add(script);
        if (script.Contains("typeof editor", StringComparison.Ordinal))
        {
            return Task.FromResult("\"object\"");
        }

        if (script.Contains("typeof window.scrollToSelection", StringComparison.Ordinal))
        {
            return Task.FromResult("\"function\"");
        }

        return Task.FromResult("ok");
    }

    public Task PostJsonAsync(string json)
    {
        PostedJson.Add(json);
        return Task.CompletedTask;
    }

    public Task PostStringAsync(string message)
    {
        PostedJson.Add(message);
        return Task.CompletedTask;
    }

    public void Focus()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void SendMessage(string body)
    {
        MessageReceived?.Invoke(this, new WebViewHostMessageReceivedEventArgs(body));
    }
}
