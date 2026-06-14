using System;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

internal static class WebViewRenderPolicy
{
    private const string LinuxWebKitGtkReason =
        "Linux WebKitGTK 内嵌 WebView 当前无法可靠绘制，已切换到后备视图。";

    public static bool ShouldUseFallback(IWebViewHost? host)
    {
        var description = host?.AdapterDescription;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        // NativeDialog is not an embedded renderer. It may show content in a separate native
        // surface, but it cannot safely participate in the editor/PDF workspace layout.
        return description.Contains("NativeDialog", StringComparison.OrdinalIgnoreCase)
            || description.Contains("IsSupported = False", StringComparison.OrdinalIgnoreCase)
            || description.Contains("IsInstalled = False", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildFallbackStatus(string surfaceName)
    {
        return $"{surfaceName} 不可用：{LinuxWebKitGtkReason}";
    }
}
