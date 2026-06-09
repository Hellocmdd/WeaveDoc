namespace WeaveDoc.MarkdownEditor.Controls.Web;

public sealed class NativeWebViewHostFactory : IWebViewHostFactory
{
    public static NativeWebViewHostFactory Instance { get; } = new();

    private NativeWebViewHostFactory()
    {
    }

    public IWebViewHost Create()
    {
        return new NativeWebViewHost();
    }
}
