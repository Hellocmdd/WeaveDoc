using System;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.Controls.Web;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls;

public partial class MonacoEditorControl : UserControl
{
    public const string DefaultFallbackStatusText =
        "Monaco 编辑器不可用：该编辑路径正在退役，请使用原生 Markdown 编辑器。";

    private IWebViewHost? _webViewHost;
    private bool _isInitialized;
    private TextBox? _fallbackEditor;
    private Border? _hostBorder;

    public static readonly StyledProperty<string> EditorContentProperty =
        AvaloniaProperty.Register<MonacoEditorControl, string>(
            nameof(EditorContent),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public MonacoEditorControl()
    {
        InitializeComponent();
        _fallbackEditor = this.FindControl<TextBox>("EditorFallbackTextBox");
        _hostBorder = this.FindControl<Border>("HostBorder");
        UpdateFallbackEditor();
    }

    public IWebViewHostFactory WebViewHostFactory { get; set; } = WebViewHostFactoryProvider.Current;

    public TimeSpan NavigationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public string EditorContent
    {
        get => GetValue(EditorContentProperty);
        set => SetValue(EditorContentProperty, value ?? string.Empty);
    }

    public static readonly StyledProperty<bool> IsUsingFallbackProperty =
        AvaloniaProperty.Register<MonacoEditorControl, bool>(nameof(IsUsingFallback), false);

    public bool IsUsingFallback
    {
        get => GetValue(IsUsingFallbackProperty);
        set => SetValue(IsUsingFallbackProperty, value);
    }

    public static readonly StyledProperty<string> FallbackStatusTextProperty =
        AvaloniaProperty.Register<MonacoEditorControl, string>(
            nameof(FallbackStatusText),
            DefaultFallbackStatusText);

    public string FallbackStatusText
    {
        get => GetValue(FallbackStatusTextProperty);
        set => SetValue(FallbackStatusTextProperty, value ?? DefaultFallbackStatusText);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EditorContentProperty)
        {
            UpdateFallbackEditor();

            if (_isInitialized && !IsUsingFallback)
            {
                _ = PostEditorContentAsync(EditorContent);
            }
        }
        else if (change.Property == IsUsingFallbackProperty)
        {
            UpdateFallbackEditor();
        }
    }

    public void SetContentAsync(string content)
    {
        EditorContent = content;
    }

    public Task ScrollToLineAsync(int lineNumber)
    {
        return Task.CompletedTask;
    }

    public Task ScrollToPositionAsync(int lineNumber, int column, int selectionLength = 1)
    {
        return Task.CompletedTask;
    }

    public Task ClearHighlightAsync()
    {
        return Task.CompletedTask;
    }

    public Task RequestCurrentSelectionAsync()
    {
        return Task.CompletedTask;
    }

    public async Task Activate(bool forceReset = false)
    {
        if (_webViewHost != null)
        {
            _webViewHost.View.IsVisible = true;
            if (_isInitialized && !IsUsingFallback)
            {
                await PostEditorContentAsync(EditorContent);
            }

            return;
        }

        try
        {
            _webViewHost = WebViewHostFactory.Create();
            _webViewHost.NavigationCompleted += WebViewHost_NavigationCompleted;
            _webViewHost.MessageReceived += WebViewHost_MessageReceived;
            _webViewHost.View.IsVisible = true;
            if (_hostBorder != null)
            {
                _hostBorder.Child = _webViewHost.View;
            }

            var htmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "monaco-editor", "index.html");
            _webViewHost.Navigate(new Uri(htmlPath));
            _ = ShowFallbackIfNavigationDoesNotCompleteAsync(_webViewHost);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            await DisposeHostAsync();
            ShowFallback($"Monaco 编辑器不可用：{ex.Message}");
        }
    }

    public void Deactivate()
    {
        if (_webViewHost != null)
        {
            _webViewHost.View.IsVisible = false;
        }
    }

    private async void WebViewHost_NavigationCompleted(object? sender, WebViewHostNavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            ShowFallback("Monaco 编辑器不可用：跨平台 WebView 导航失败。");
            return;
        }

        _isInitialized = true;
        if (ShowFallbackIfNativeRenderingUnavailable())
        {
            return;
        }

        ClearFallback();
        await PostEditorContentAsync(EditorContent);
    }

    private void WebViewHost_MessageReceived(object? sender, WebViewHostMessageReceivedEventArgs args)
    {
        try
        {
            using var doc = JsonDocument.Parse(args.Body);
            var root = doc.RootElement;
            var msgType = ReadString(root, "Type") ?? ReadString(root, "type");
            var msgData = ReadString(root, "Data") ?? ReadString(root, "data");

            if (string.Equals(msgType, "contentChanged", StringComparison.Ordinal) && msgData != null)
            {
                EditorContent = msgData;
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
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
            ShowFallback("Monaco 编辑器不可用：跨平台 WebView 导航超时。");
        }
    }

    private bool ShowFallbackIfNativeRenderingUnavailable()
    {
        var description = _webViewHost?.AdapterDescription ?? string.Empty;
        if (description.Contains("NativeDialog", StringComparison.OrdinalIgnoreCase))
        {
            ShowFallback("Monaco 编辑器不可用：当前 WebKitGTK 只支持 NativeDialog，不能承载内嵌编辑器。");
            return true;
        }

        return false;
    }

    private async Task PostEditorContentAsync(string content)
    {
        if (_webViewHost == null || IsUsingFallback)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { Type = "setContent", Data = content ?? string.Empty });
        await _webViewHost.PostJsonAsync(payload);
    }

    private void ShowFallback(string message)
    {
        FallbackStatusText = string.IsNullOrWhiteSpace(message) ? DefaultFallbackStatusText : message;
        IsUsingFallback = true;
        UpdateFallbackEditor();
    }

    private void ClearFallback()
    {
        IsUsingFallback = false;
        FallbackStatusText = DefaultFallbackStatusText;
        UpdateFallbackEditor();
    }

    private void UpdateFallbackEditor()
    {
        _fallbackEditor ??= this.FindControl<TextBox>("EditorFallbackTextBox");
        _hostBorder ??= this.FindControl<Border>("HostBorder");

        if (_fallbackEditor != null)
        {
            _fallbackEditor.IsVisible = IsUsingFallback;
            if (_fallbackEditor.Text != EditorContent)
            {
                _fallbackEditor.Text = EditorContent;
            }
        }

        if (_hostBorder != null)
        {
            _hostBorder.IsVisible = !IsUsingFallback;
        }
    }

    private async Task DisposeHostAsync()
    {
        if (_webViewHost == null)
        {
            return;
        }

        var host = _webViewHost;
        _webViewHost = null;
        _isInitialized = false;
        host.NavigationCompleted -= WebViewHost_NavigationCompleted;
        host.MessageReceived -= WebViewHost_MessageReceived;
        await host.DisposeAsync();
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value)
            ? value.GetString()
            : null;
    }
}
