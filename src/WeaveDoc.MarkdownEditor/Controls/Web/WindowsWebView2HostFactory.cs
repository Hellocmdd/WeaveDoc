namespace WeaveDoc.MarkdownEditor.Controls.Web;

public sealed class WindowsWebView2HostFactory : IWebViewHostFactory
{
    public static WindowsWebView2HostFactory Instance { get; } = new();

    private WindowsWebView2HostFactory()
    {
    }

    public IWebViewHost Create()
    {
        return new WindowsWebView2Host();
    }
}
