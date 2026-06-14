using System;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

public static class WebViewHostFactoryProvider
{
    public static IWebViewHostFactory Current { get; set; } = CreateDefaultFactory();

    public static void Reset()
    {
        Current = CreateDefaultFactory();
    }

    private static IWebViewHostFactory CreateDefaultFactory()
    {
        return OperatingSystem.IsWindows()
            ? WindowsWebView2HostFactory.Instance
            : NativeWebViewHostFactory.Instance;
    }
}
