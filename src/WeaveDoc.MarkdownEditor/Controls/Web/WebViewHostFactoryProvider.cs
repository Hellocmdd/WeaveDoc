namespace WeaveDoc.MarkdownEditor.Controls.Web;

public static class WebViewHostFactoryProvider
{
    public static IWebViewHostFactory Current { get; set; } = NativeWebViewHostFactory.Instance;

    public static void Reset()
    {
        Current = NativeWebViewHostFactory.Instance;
    }
}
