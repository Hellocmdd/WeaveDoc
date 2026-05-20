using System.Text.Json;

namespace WeaveDoc.MarkdownEditor.Services.Interop
{
    internal class WebViewInterop
    {
        // …existing code…

        private void OnWebMessageReceived(object? sender, dynamic e)
        {
            // 兼容不同 WebView2/Blazor 事件类型，防止 null 引发警告/异常
            string json = string.Empty;
            try
            {
                json = e?.WebMessageAsJson ?? e?.Message ?? string.Empty;
            }
            catch { json = string.Empty; }

            if (string.IsNullOrWhiteSpace(json)) return;

            var message = JsonSerializer.Deserialize<JsMessage?>(json);
            // ...处理 message ...
        }

        private record JsMessage(string? Type, object? Data);
    }
}