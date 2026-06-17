using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class VersionHistoryDialog : Window
{
    private readonly DocumentWorkspaceViewModel? _workspace;
    private readonly MarkdownDocumentService _markdownDocumentService = new();

    private string? _pendingDeleteSnapshotId;

    public VersionHistoryDialog() : this(null, ShellThemeKind.Dark)
    {
    }

    public VersionHistoryDialog(DocumentWorkspaceViewModel? workspace)
        : this(workspace, ShellThemeKind.Dark)
    {
    }

    public VersionHistoryDialog(DocumentWorkspaceViewModel? workspace, ShellThemeKind theme)
    {
        _workspace = workspace;
        InitializeComponent();
        // 对齐主页面 Markdown 预览：把 WebView 的浅/深色随应用主题切换。
        // ShellThemeKind.ToString() -> "Dark"/"Light"，PreviewWebViewControl 据此切换配色。
        SnapshotPreviewControl.ViewerCssTheme = theme.ToString();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        TitleText.Text = $"文档快照 - {_workspace?.DisplayName ?? "未打开文档"}";
        SubtitleText.Text = _workspace?.CurrentFilePath ?? string.Empty;
        await LoadSnapshotsAsync();
    }

    private async Task LoadSnapshotsAsync()
    {
        RestoreButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
        _pendingDeleteSnapshotId = null;
        DeleteButton.Content = "删除";
        SnapshotPreviewControl.HtmlContent = string.Empty;
        SnapshotPreviewControl.SourceFilePath = _workspace?.CurrentFilePath;

        if (_workspace?.HasDocument != true)
        {
            SnapshotList.ItemsSource = Array.Empty<SnapshotListItem>();
            StatusText.Text = "请先打开 Markdown 文档";
            return;
        }

        try
        {
            var snapshots = await _workspace.ListSnapshotsAsync();
            SnapshotList.ItemsSource = snapshots.Select(SnapshotListItem.FromEntry).ToArray();
            StatusText.Text = snapshots.Count == 0
                ? "暂无可恢复快照"
                : $"共 {snapshots.Count} 个快照";
        }
        catch (Exception ex)
        {
            SnapshotList.ItemsSource = Array.Empty<SnapshotListItem>();
            StatusText.Text = $"加载文档快照失败：{ex.Message}";
        }
    }

    private async void OnSnapshotSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var hasSelection = SnapshotList.SelectedItem is SnapshotListItem;
        RestoreButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        // 切换选中项时撤销待删除武装态，避免误删新选中的快照。
        _pendingDeleteSnapshotId = null;
        DeleteButton.Content = "删除";
        if (SnapshotList.SelectedItem is not SnapshotListItem item || _workspace is null)
        {
            SnapshotPreviewControl.HtmlContent = string.Empty;
            return;
        }

        try
        {
            var markdown = await _workspace.ReadSnapshotContentAsync(item.SnapshotId);
            var preview = _markdownDocumentService.CreatePreview(markdown, _workspace.CurrentFilePath);
            SnapshotPreviewControl.HtmlContent = preview.Succeeded
                ? preview.PreviewHtml
                : $"<p>{System.Net.WebUtility.HtmlEncode(preview.ErrorMessage ?? "生成快照预览失败")}</p>";
            SnapshotPreviewControl.SourceFilePath = _workspace.CurrentFilePath;
            StatusText.Text = item.DetailText;
        }
        catch (Exception ex)
        {
            SnapshotPreviewControl.HtmlContent = string.Empty;
            StatusText.Text = $"读取快照失败：{ex.Message}";
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await LoadSnapshotsAsync();
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (SnapshotList.SelectedItem is not SnapshotListItem item || _workspace is null)
        {
            return;
        }

        // 两段式确认（对齐 SettingsDialog.OnDeleteClick）：
        // 第一次点击仅武装，改文案提示需再次确认；第二次点击才真正删除。
        if (_pendingDeleteSnapshotId != item.SnapshotId)
        {
            _pendingDeleteSnapshotId = item.SnapshotId;
            DeleteButton.Content = "确认删除此快照？";
            return;
        }

        _pendingDeleteSnapshotId = null;
        DeleteButton.IsEnabled = false;
        StatusText.Text = "正在删除快照...";

        var deleted = await _workspace.DeleteSnapshotAsync(item.SnapshotId);
        if (deleted)
        {
            await LoadSnapshotsAsync();
            return;
        }

        DeleteButton.IsEnabled = true;
        DeleteButton.Content = "删除";
        StatusText.Text = _workspace.ErrorMessage ?? "删除快照失败";
    }

    private async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (SnapshotList.SelectedItem is not SnapshotListItem item || _workspace is null)
        {
            return;
        }

        RestoreButton.IsEnabled = false;
        StatusText.Text = "正在恢复快照...";
        var restored = await _workspace.RestoreSnapshotAsync(item.SnapshotId);
        if (restored)
        {
            Close(true);
            return;
        }

        RestoreButton.IsEnabled = true;
        StatusText.Text = _workspace.ErrorMessage ?? "恢复快照失败";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private sealed record SnapshotListItem(
        string SnapshotId,
        string CreatedAtText,
        string DetailText)
    {
        public static SnapshotListItem FromEntry(DocumentSnapshotEntry entry)
        {
            var triggerText = entry.Trigger switch
            {
                SnapshotTrigger.AutoSave => "自动保存",
                SnapshotTrigger.ManualSave => "手动保存",
                SnapshotTrigger.RestoreBeforeOverwrite => "恢复前保护",
                SnapshotTrigger.CloseBeforeUnsaved => "关闭前保护",
                _ => "快照"
            };

            return new SnapshotListItem(
                entry.SnapshotId,
                entry.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                $"{triggerText} · {FormatBytes(entry.SnapshotSizeBytes)} · {entry.ContentLength:N0} 字符");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            var kib = bytes / 1024d;
            return kib < 1024 ? $"{kib:0.#} KB" : $"{kib / 1024d:0.#} MB";
        }
    }
}
