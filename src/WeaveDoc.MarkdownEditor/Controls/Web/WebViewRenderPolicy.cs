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

        // NativeDialog: WebView renders as a floating dialog (not embedded).
        // The content IS visible — just sized differently. Do not fallback.
        // Only fallback when the adapter genuinely cannot render.
        return description.Contains("IsSupported = False", StringComparison.OrdinalIgnoreCase)
            || description.Contains("IsInstalled = False", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildFallbackStatus(string surfaceName)
    {
        return $"{surfaceName} 不可用：{LinuxWebKitGtkReason}";
    }
}
