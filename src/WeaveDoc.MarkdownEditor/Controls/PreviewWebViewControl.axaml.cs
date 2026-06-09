using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.MarkdownEditor.Controls.Web;
using WeaveDoc.MarkdownEditor.Helpers;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class PreviewWebViewControl : UserControl
    {
        public const string DefaultFallbackStatusText =
            "HTML 预览不可用：跨平台 WebView 未初始化。请确认系统 WebKit/WPE 运行库可用。";

        private IWebViewHost? _webViewHost;
        private bool _isInitialized;
        private bool _isActive;
        private bool _isInitializing;
        private string _pendingContent = string.Empty;
        private Action? _notifyPreviewReadyCallback;

        public PreviewWebViewControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public IWebViewHostFactory WebViewHostFactory { get; set; } = WebViewHostFactoryProvider.Current;

        public TimeSpan NavigationTimeout { get; set; } = TimeSpan.FromSeconds(5);

        private IMarkdownEditorHost? Host =>
            this.GetVisualAncestors().OfType<IMarkdownEditorHost>().FirstOrDefault()
            ?? VisualRoot as IMarkdownEditorHost;

        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, string>(
                nameof(HtmlContent),
                string.Empty,
                defaultBindingMode: BindingMode.OneWay);

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<bool> IsUsingFallbackProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, bool>(nameof(IsUsingFallback), false);

        public bool IsUsingFallback
        {
            get => GetValue(IsUsingFallbackProperty);
            set => SetValue(IsUsingFallbackProperty, value);
        }

        public static readonly StyledProperty<string> FallbackStatusTextProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, string>(
                nameof(FallbackStatusText),
                DefaultFallbackStatusText);

        public string FallbackStatusText
        {
            get => GetValue(FallbackStatusTextProperty);
            set => SetValue(FallbackStatusTextProperty, value ?? DefaultFallbackStatusText);
        }

        public static readonly StyledProperty<string> FallbackContentTextProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, string>(
                nameof(FallbackContentText),
                string.Empty);

        public string FallbackContentText
        {
            get => GetValue(FallbackContentTextProperty);
            set => SetValue(FallbackContentTextProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<bool> AutoActivateOnVisibleProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, bool>(nameof(AutoActivateOnVisible), true);

        public bool AutoActivateOnVisible
        {
            get => GetValue(AutoActivateOnVisibleProperty);
            set => SetValue(AutoActivateOnVisibleProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == HtmlContentProperty)
            {
                var content = change.NewValue as string ?? string.Empty;
                UpdateFallbackContent(content);
                UpdatePreview(content);
            }
            else if (change.Property == IsVisibleProperty)
            {
                if (IsVisible && AutoActivateOnVisible)
                {
                    _ = Activate(false);
                }
                else
                {
                    Deactivate();
                }
            }
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (IsVisible && AutoActivateOnVisible)
            {
                await Activate(false);
            }
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            Deactivate();
        }

        private async Task<bool> EnsureWebViewAsync()
        {
            if (_webViewHost != null)
            {
                if (!_isInitialized)
                {
                    NavigateToPreview();
                }
                return true;
            }

            if (_isInitializing)
            {
                return false;
            }

            _isInitializing = true;
            try
            {
                _webViewHost = WebViewHostFactory.Create();
                _webViewHost.NavigationCompleted += WebViewHost_NavigationCompleted;
                _webViewHost.MessageReceived += WebViewHost_MessageReceived;
                GetWebViewContainer().Children.Add(_webViewHost.View);
                _webViewHost.View.IsVisible = _isActive;

                NavigateToPreview();
                if (!IsUsingFallback)
                {
                    ClearFallback();
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                await DisposeHostAsync();
                ShowFallback($"HTML 预览不可用：{ex.Message}");
                return false;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void NavigateToPreview()
        {
            if (_webViewHost == null || IsUsingFallback)
            {
                return;
            }

            var htmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "preview-template.html");
            _webViewHost.Navigate(new Uri(htmlPath));
            _ = ShowFallbackIfNavigationDoesNotCompleteAsync(_webViewHost);
        }

        private async Task ShowFallbackIfNavigationDoesNotCompleteAsync(IWebViewHost host)
        {
            if (NavigationTimeout <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(NavigationTimeout).ConfigureAwait(true);

            if (_webViewHost == host && !_isInitialized)
            {
                ShowFallback("HTML 预览不可用：跨平台 WebView 导航超时。");
            }
        }

        private async void WebViewHost_NavigationCompleted(object? sender, WebViewHostNavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                ShowFallback("HTML 预览不可用：跨平台 WebView 导航失败。");
                return;
            }

            var contentToApply = string.IsNullOrEmpty(_pendingContent) ? HtmlContent : _pendingContent;
            _pendingContent = string.Empty;
            UpdateFallbackContent(contentToApply);

            _isInitialized = true;
            if (ShowFallbackIfNativeRenderingUnavailable())
            {
                NotifyPreviewReady();
                return;
            }

            ClearFallback();
            await UpdatePreviewAsync(contentToApply);
        }

        private void WebViewHost_MessageReceived(object? sender, WebViewHostMessageReceivedEventArgs args)
        {
            try
            {
                if (!TryReadMessage(args.Body, out var msgType, out var msgData))
                {
                    return;
                }

                if (msgType == "previewSelection" && msgData != null)
                {
                    HandlePreviewSelection(msgData);
                }
                else if (msgType == "previewClick" && msgData != null)
                {
                    HandlePreviewClickMessage(msgData);
                }
                else if (msgType == "previewClearHighlight")
                {
                    Host?.ClearEditorHighlight();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void HandlePreviewSelection(string msgData)
        {
            try
            {
                using var selectionData = JsonDocument.Parse(msgData);
                var root = selectionData.RootElement;
                var startLine = ReadInt(root, "startLine", 1);
                var startColumn = ReadInt(root, "startColumn", 1);
                var selectionLength = ReadInt(root, "length", 1);

                Host?.ScrollEditorToPositionWithRange(startLine, startColumn, selectionLength);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void HandlePreviewClickMessage(string msgData)
        {
            try
            {
                using var clickData = JsonDocument.Parse(msgData);
                var root = clickData.RootElement;
                var clickedLine = ReadInt(root, "line", 1);
                var clickedColumn = ReadInt(root, "column", 1);

                Host?.ScrollEditorToPosition(clickedLine, clickedColumn);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void UpdatePreview(string content)
        {
            _ = UpdatePreviewAsync(content);
        }

        private async Task UpdatePreviewAsync(string content)
        {
            try
            {
                UpdateFallbackContent(content);
                if (IsUsingFallback)
                {
                    return;
                }

                if (_webViewHost == null || !_isInitialized)
                {
                    _pendingContent = content ?? string.Empty;
                    return;
                }

                _pendingContent = content ?? string.Empty;
                await WaitForJavaScriptReadyAsync();
                var script = $"window.updateContent({JsonSerializer.Serialize(_pendingContent)});";
                await _webViewHost.InvokeScriptAsync(script);
                NotifyPreviewReady();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                ShowFallback($"HTML 预览不可用：{ex.Message}");
            }
        }

        private void ShowFallback(string? statusText = null)
        {
            FallbackStatusText = string.IsNullOrWhiteSpace(statusText)
                ? DefaultFallbackStatusText
                : statusText;
            UpdateFallbackContent(HtmlContent);
            IsUsingFallback = true;

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = false;
            }
        }

        private void ClearFallback()
        {
            IsUsingFallback = false;
            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = _isActive;
            }
        }

        private bool ShowFallbackIfNativeRenderingUnavailable()
        {
            if (!WebViewRenderPolicy.ShouldUseFallback(_webViewHost))
            {
                return false;
            }

            ShowFallback(WebViewRenderPolicy.BuildFallbackStatus("HTML 预览"));
            return true;
        }

        private void UpdateFallbackContent(string content)
        {
            FallbackContentText = ConvertHtmlToFallbackText(content);
        }

        public static string ConvertHtmlToFallbackText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(html.Length);
            var tag = new StringBuilder();
            var inTag = false;

            foreach (var character in html)
            {
                if (character == '<')
                {
                    inTag = true;
                    tag.Clear();
                    continue;
                }

                if (inTag)
                {
                    if (character == '>')
                    {
                        AppendLineBreakForTag(tag.ToString(), builder);
                        inTag = false;
                    }
                    else
                    {
                        tag.Append(character);
                    }

                    continue;
                }

                builder.Append(character);
            }

            var decoded = WebUtility.HtmlDecode(builder.ToString()) ?? string.Empty;
            decoded = decoded.Replace('\u00a0', ' ');
            var lines = decoded
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => Regex.Replace(line.Trim(), "[ \t]+", " "))
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join(Environment.NewLine, lines);
        }

        private static void AppendLineBreakForTag(string rawTag, StringBuilder builder)
        {
            var tag = rawTag.Trim().TrimStart('/');
            var separatorIndex = tag.IndexOfAny([' ', '\t', '\r', '\n', '/']);
            if (separatorIndex >= 0)
            {
                tag = tag[..separatorIndex];
            }

            if (tag.Equals("br", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("p", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("div", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("li", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(tag, "^h[1-6]$", RegexOptions.IgnoreCase))
            {
                builder.AppendLine();
            }
        }

        private void NotifyPreviewReady()
        {
            if (_notifyPreviewReadyCallback == null)
            {
                return;
            }

            _notifyPreviewReadyCallback();
            _notifyPreviewReadyCallback = null;
        }

        public void SetOnPreviewReadyCallback(Action callback)
        {
            _notifyPreviewReadyCallback = callback;
        }

        public void HandlePreviewClick(int line, int column)
        {
            Host?.ScrollEditorToPosition(line, column);
        }

        public void SetContent(string content)
        {
            HtmlContent = content;
        }

        public async void ScrollToLine(int lineNumber)
        {
            var script = $@"
                (function() {{
                    if (window.clearHighlight) {{
                        window.clearHighlight();
                    }}

                    var targetLine = {lineNumber};
                    var targetElement = null;
                    var allElements = document.querySelectorAll('[data-line]');

                    for (var i = 0; i < allElements.length; i++) {{
                        var el = allElements[i];
                        var elLine = parseInt(el.getAttribute('data-line'), 10);

                        if (elLine === targetLine) {{
                            targetElement = el;
                            break;
                        }}
                        if (elLine > targetLine) {{
                            targetElement = i > 0 ? allElements[i - 1] : el;
                            break;
                        }}
                    }}

                    if (!targetElement && allElements.length > 0) {{
                        targetElement = allElements[allElements.length - 1];
                    }}

                    if (targetElement) {{
                        targetElement.classList.add('highlight-line');
                        targetElement.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                    }}
                }})();
            ";

            await InvokePreviewScriptAsync(script);
        }

        public async void ScrollToSelection(int startLine, int startCol, int endLine, int endCol)
        {
            await WaitForJavaScriptReadyAsync();
            await InvokePreviewScriptAsync($"window.scrollToSelection({startLine}, {startCol}, {endLine}, {endCol});");
        }

        private async Task WaitForJavaScriptReadyAsync()
        {
            if (_webViewHost == null)
            {
                return;
            }

            const int maxAttempts = 20;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var result = await _webViewHost.InvokeScriptAsync("typeof window.updateContent");
                    if (result == "\"function\"" || result == "function")
                    {
                        return;
                    }
                }
                catch
                {
                }

                await Task.Delay(50);
            }
        }

        public async Task Activate(bool forceReset = false)
        {
            if (_isActive && !forceReset && _webViewHost != null && _isInitialized)
            {
                return;
            }

            _isActive = true;
            if (!await EnsureWebViewAsync())
            {
                return;
            }

            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = true;

                // Yield to the Avalonia dispatcher at Render priority so that any
                // pending Layout-priority work (which commits the new control bounds
                // to WebKitGTK's offscreen renderer) completes before we run scripts.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

                if (ShowFallbackIfNativeRenderingUnavailable())
                {
                    return;
                }

                try
                {
                    // Force WebKitGTK offscreen to repaint by triggering a resize event.
                    await _webViewHost.InvokeScriptAsync("window.dispatchEvent(new Event('resize'));");
                }
                catch { }

                if (forceReset || !_isInitialized)
                {
                    _isInitialized = false;
                    NavigateToPreview();
                }
                else
                {
                    ClearFallback();
                    await UpdatePreviewAsync(HtmlContent);
                }
            }
        }

        public void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            if (_webViewHost != null)
            {
                _webViewHost.View.IsVisible = false;
            }
        }

        private async Task InvokePreviewScriptAsync(string script)
        {
            try
            {
                if (_webViewHost == null || !_isInitialized || IsUsingFallback)
                {
                    return;
                }

                await _webViewHost.InvokeScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                ShowFallback($"HTML 预览不可用：{ex.Message}");
            }
        }

        private async Task DisposeHostAsync()
        {
            if (_webViewHost == null)
            {
                return;
            }

            _webViewHost.NavigationCompleted -= WebViewHost_NavigationCompleted;
            _webViewHost.MessageReceived -= WebViewHost_MessageReceived;
            TryGetWebViewContainer()?.Children.Remove(_webViewHost.View);
            await _webViewHost.DisposeAsync();
            _webViewHost = null;
            _isInitialized = false;
        }

        private Grid GetWebViewContainer()
        {
            return TryGetWebViewContainer()
                ?? throw new InvalidOperationException("HTML 预览不可用：WebView 容器未初始化。");
        }

        private Grid? TryGetWebViewContainer()
        {
            return WebViewContainer ?? this.FindControl<Grid>("WebViewContainer");
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private static bool TryReadMessage(string body, out string? messageType, out string? messageData)
        {
            messageType = null;
            messageData = null;

            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                using var nested = JsonDocument.Parse(root.GetString() ?? string.Empty);
                return TryReadMessage(nested.RootElement, out messageType, out messageData);
            }

            return TryReadMessage(root, out messageType, out messageData);
        }

        private static bool TryReadMessage(JsonElement root, out string? messageType, out string? messageData)
        {
            messageType = ReadString(root, "Type") ?? ReadString(root, "type");
            messageData = ReadData(root, "Data") ?? ReadData(root, "data");
            return !string.IsNullOrWhiteSpace(messageType);
        }

        private static string? ReadString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static string? ReadData(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.GetRawText();
        }

        private static int ReadInt(JsonElement root, string propertyName, int fallback)
        {
            return root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
                ? value
                : fallback;
        }
    }
}
