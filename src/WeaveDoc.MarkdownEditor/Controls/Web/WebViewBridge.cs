using System.Text.Json;

namespace WeaveDoc.MarkdownEditor.Controls.Web;

public static class WebViewBridge
{
    public const string Script = """
        (() => {
            if (globalThis.weaveDocBridge) {
                return;
            }

            const listeners = [];

            const normalize = message => {
                if (typeof message === "string") {
                    try {
                        return JSON.parse(message);
                    } catch {
                        return { Type: "raw", Data: message };
                    }
                }

                return message || {};
            };

            const send = message => {
                const normalized = normalize(message);
                try {
                    if (globalThis.chrome?.webview?.postMessage) {
                        globalThis.chrome.webview.postMessage(normalized);
                        return;
                    }

                    if (globalThis.external?.invoke) {
                        globalThis.external.invoke(JSON.stringify(normalized));
                        return;
                    }

                    if (globalThis.webkit?.messageHandlers?.webview?.postMessage) {
                        globalThis.webkit.messageHandlers.webview.postMessage(normalized);
                        return;
                    }

                    globalThis.postMessage(JSON.stringify(normalized), "*");
                } catch (error) {
                    console.error("weaveDocBridge post failed", error);
                }
            };

            const receiveFromHost = message => {
                const normalized = normalize(message);
                for (const listener of listeners.slice()) {
                    try {
                        listener(normalized);
                    } catch (error) {
                        console.error("weaveDocBridge listener failed", error);
                    }
                }
            };

            globalThis.weaveDocBridge = {
                post: send,
                onHostMessage: listener => {
                    if (typeof listener === "function") {
                        listeners.push(listener);
                    }
                },
                receiveFromHost
            };

            if (globalThis.chrome?.webview?.addEventListener) {
                globalThis.chrome.webview.addEventListener("message", event => receiveFromHost(event.data));
            }

            globalThis.addEventListener("message", event => receiveFromHost(event.data));
        })();
        """;

    public static string BuildReceiveScript(string json)
    {
        return $"if (globalThis.weaveDocBridge && typeof globalThis.weaveDocBridge.receiveFromHost === \"function\") {{ globalThis.weaveDocBridge.receiveFromHost({json}); }}";
    }

    public static string BuildReceiveStringScript(string message)
    {
        return BuildReceiveScript(JsonSerializer.Serialize(message));
    }
}
