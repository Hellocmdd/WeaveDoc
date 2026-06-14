using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using System.Linq;
using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.App.ViewModels;
using WeaveDoc.App.Views;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Controls.Web;
using WeaveDoc.Rag.Models;
using Xunit;

namespace WeaveDoc.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public void ShellApp_InitializesAvaloniaEditFluentTheme()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var appAxaml = File.ReadAllText(Path.Combine(repoRoot, "src/WeaveDoc.App/App.axaml"));

        Assert.Contains("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml", appAxaml);
    }

    [AvaloniaFact]
    public async Task MainWindow_UsesShellViewModelWithEmptyDefaults()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
            Assert.Equal(EditorSurfaceMode.Edit, viewModel.EditorMode);
            Assert.Equal(ShellThemeKind.Dark, viewModel.Theme);
            Assert.Equal(AiPanelTabKind.Chat, viewModel.SelectedAiPanelTab);
            Assert.True(viewModel.IsAiPanelExpanded);
            Assert.Empty(viewModel.Documents);
            Assert.Equal("未打开 Markdown 文档", viewModel.CurrentDocumentTitle);
            Assert.Equal("暂无打开的文档", viewModel.EmptyDocumentText);
            Assert.Equal("暂无可预览内容", viewModel.EmptyPreviewText);
            Assert.Equal("暂无问答记录", viewModel.EmptyAiConversationText);

            Assert.NotNull(window.FindControl<Grid>("ShellRoot"));
            Assert.NotNull(window.FindControl<Border>("ShellTitleBar"));
            Assert.NotNull(window.FindControl<Border>("ShellCommandBar"));
            Assert.Null(window.FindControl<Border>("ShellMenuBar"));
            Assert.Null(window.FindControl<Border>("ShellToolbar"));
            Assert.NotNull(window.FindControl<Grid>("ShellWorkspace"));
            Assert.Null(window.FindControl<Control>("ShellNavigationRailControl"));
            Assert.NotNull(window.FindControl<WorkspaceSidebar>("WorkspaceSidebarControl"));
            Assert.NotNull(window.FindControl<EditorWorkspace>("EditorWorkspaceControl"));
            Assert.NotNull(window.FindControl<AiAssistantPanel>("AiAssistantPanelControl"));
            Assert.NotNull(window.FindControl<ShellStatusBar>("ShellStatusBarControl"));
        });
    }

    [AvaloniaFact]
    public async Task MainWindow_UsesFinalShellVisualAnchors()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var titleBar = Find<Border>(window, "ShellTitleBar");
            var commandBar = Find<Border>(window, "ShellCommandBar");
            var workspace = Find<Grid>(window, "ShellWorkspace");
            var sidebar = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl");
            var editor = Find<EditorWorkspace>(window, "EditorWorkspaceControl");
            var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");
            var statusBar = Find<ShellStatusBar>(window, "ShellStatusBarControl");

            Assert.NotNull(titleBar.Background);
            Assert.NotNull(commandBar.Background);
            Assert.Null(window.FindControl<Border>("ShellMenuBar"));
            Assert.Null(window.FindControl<Border>("ShellToolbar"));
            Assert.NotNull(workspace.Background);
            Assert.Null(window.FindControl<Control>("ShellNavigationRailControl"));
            Assert.NotNull(sidebar.FindControl<Grid>("WorkspaceSidebarRoot")?.Background);
            Assert.NotNull(sidebar.FindControl<Border>("DocumentPreviewToolbar")?.Background);
            Assert.NotNull(sidebar.FindControl<Border>("DocumentPreviewTabStrip")?.Background);
            Assert.NotNull(sidebar.FindControl<Border>("DocumentPreviewCanvas")?.Background);
            Assert.NotNull(sidebar.FindControl<Border>("DocumentPreviewPaper")?.Background);
            Assert.NotNull(sidebar.FindControl<TextBlock>("DocumentPreviewEmptyStateText"));
            Assert.NotNull(editor.FindControl<Grid>("EditorWorkspaceRoot")?.Background);
            Assert.NotNull(aiPanel.FindControl<Grid>("AiAssistantPanelRoot")?.Background);
            Assert.NotNull(statusBar.FindControl<Border>("ShellStatusBarRoot")?.Background);
            Assert.NotNull(window.FindControl<GridSplitter>("LeftWorkspaceSplitter"));
            Assert.NotNull(window.FindControl<GridSplitter>("RightWorkspaceSplitter"));
        });
    }

    [AvaloniaFact]
    public async Task MainWindow_MinimumSizeKeepsCoreRegionsVisible()
    {
        var window = new MainWindow
        {
            Width = 1120,
            Height = 680
        };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ArrangeWindow(window);

            AssertVisibleSize(Find<Border>(window, "ShellTitleBar"), 600, 24);
            AssertVisibleSize(Find<Border>(window, "ShellCommandBar"), 600, 28);
            AssertVisibleSize(Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl"), 300, 300);
            AssertVisibleSize(Find<EditorWorkspace>(window, "EditorWorkspaceControl"), 400, 300);
            AssertVisibleSize(Find<AiAssistantPanel>(window, "AiAssistantPanelControl"), 280, 300);
            AssertVisibleSize(Find<ShellStatusBar>(window, "ShellStatusBarControl"), 600, 18);
        });
    }

    [AvaloniaFact]
    public async Task MainWindow_UsesResizableWorkspaceColumnsWithMinimums()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var workspace = Find<Grid>(window, "ShellWorkspace");
            var columns = workspace.ColumnDefinitions;

            Assert.Equal(300, columns[0].MinWidth);
            Assert.Equal(4, columns[1].Width.Value);
            Assert.Equal(400, columns[2].MinWidth);
            Assert.Equal(4, columns[3].Width.Value);
            Assert.Equal(280, columns[4].MinWidth);

            Assert.Equal(GridResizeDirection.Columns, Find<GridSplitter>(window, "LeftWorkspaceSplitter").ResizeDirection);
            Assert.Equal(GridResizeBehavior.PreviousAndNext, Find<GridSplitter>(window, "LeftWorkspaceSplitter").ResizeBehavior);
            Assert.Equal(GridResizeDirection.Columns, Find<GridSplitter>(window, "RightWorkspaceSplitter").ResizeDirection);
            Assert.Equal(GridResizeBehavior.PreviousAndNext, Find<GridSplitter>(window, "RightWorkspaceSplitter").ResizeBehavior);
            Assert.False(Find<GridSplitter>(window, "LeftWorkspaceSplitter").ShowsPreview);
            Assert.False(Find<GridSplitter>(window, "RightWorkspaceSplitter").ShowsPreview);
        });
    }

    [AvaloniaFact]
    public async Task WorkspaceSplitters_CanBeDraggedAndKeepColumnMinimums()
    {
        var window = new MainWindow
        {
            Width = 1280,
            Height = 820
        };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var workspace = Find<Grid>(window, "ShellWorkspace");
            var leftSplitter = Find<GridSplitter>(window, "LeftWorkspaceSplitter");
            var rightSplitter = Find<GridSplitter>(window, "RightWorkspaceSplitter");

            ArrangeWindow(window);
            AssertWorkspaceColumnMinimums(window, workspace);
            AssertWorkspaceLayoutIsStable(window, workspace);

            var initialLeftWidth = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl").Bounds.Width;
            DragSplitter(window, leftSplitter, 110);
            ArrangeWindow(window);

            var widenedLeftWidth = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl").Bounds.Width;
            Assert.True(widenedLeftWidth > initialLeftWidth + 20, $"left column width stayed at {widenedLeftWidth}");
            AssertWorkspaceColumnMinimums(window, workspace);
            AssertWorkspaceLayoutIsStable(window, workspace);

            DragSplitter(window, leftSplitter, -900);
            ArrangeWindow(window);
            AssertWorkspaceColumnMinimums(window, workspace);
            AssertWorkspaceLayoutIsStable(window, workspace);

            var initialAiWidth = Find<AiAssistantPanel>(window, "AiAssistantPanelControl").Bounds.Width;
            DragSplitter(window, rightSplitter, -90);
            ArrangeWindow(window);

            var resizedAiWidth = Find<AiAssistantPanel>(window, "AiAssistantPanelControl").Bounds.Width;
            Assert.True(Math.Abs(resizedAiWidth - initialAiWidth) > 20, $"AI column width stayed at {resizedAiWidth}");
            AssertWorkspaceColumnMinimums(window, workspace);
            AssertWorkspaceLayoutIsStable(window, workspace);

            DragSplitter(window, rightSplitter, 900);
            ArrangeWindow(window);
            AssertWorkspaceColumnMinimums(window, workspace);
            AssertWorkspaceLayoutIsStable(window, workspace);
        });
    }

    [AvaloniaFact]
    public async Task ShellVisualResources_AreLoadedByTestHost()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AssertBrushResource("ShellBackgroundBrush");
            AssertBrushResource("ShellPanelBrush");
            AssertBrushResource("ShellEditorBackgroundBrush");
            AssertBrushResource("ShellAccentBrush");
            AssertBrushResource("ShellBorderBrush");

            var root = Find<Grid>(window, "ShellRoot");
            var documentPreviewRoot = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl")
                .FindControl<Grid>("WorkspaceSidebarRoot");
            var editorRoot = Find<EditorWorkspace>(window, "EditorWorkspaceControl")
                .FindControl<Grid>("EditorWorkspaceRoot");

            Assert.NotNull(root.Background);
            Assert.NotNull(documentPreviewRoot?.Background);
            Assert.NotNull(editorRoot?.Background);
        });
    }

    [AvaloniaFact]
    public async Task LeftDocumentPreview_MatchesDemoSkeletonWithEmptyState()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var preview = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl");

            Assert.NotNull(preview.FindControl<Border>("DocumentPreviewToolbar"));
            Assert.NotNull(preview.FindControl<Border>("DocumentPreviewTabStrip"));
            Assert.NotNull(preview.FindControl<Border>("DocumentPreviewCanvas"));
            Assert.NotNull(preview.FindControl<Border>("DocumentPreviewPaper"));

            Assert.Equal("0 / 0", Find<TextBlock>(preview, "DocumentPreviewPageText").Text);
            Assert.Equal("100%", Find<TextBlock>(preview, "DocumentPreviewZoomText").Text);
            Assert.Equal("从“打开”或拖拽 Markdown / PDF 到此处开始",
                Find<TextBlock>(preview, "DocumentPreviewEmptyStateText").Text);
        });
    }

    [AvaloniaFact]
    public async Task ShellControls_UpdateLocalStateOnly()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
            var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
            var editModeButton = Find<Button>(editor, "EditModeButton");
            var previewModeButton = Find<Button>(editor, "PreviewModeButton");
            var aiButton = Find<Button>(window, "AiShellCommandButton");
            var themeButton = Find<Button>(window, "ThemeMenuButton");
            var aiChatButton = Find<Button>(window, "AiChatCommandButton");
            var editorEmptyState = Find<Grid>(editor, "EditorEmptyState");
            var previewEmptyState = Find<Grid>(editor, "PreviewEmptyState");
            var markdownPreview = Find<PreviewWebViewControl>(editor, "MarkdownPreviewControl");

            Assert.Contains("active", editModeButton.Classes);
            Assert.DoesNotContain("active", previewModeButton.Classes);
            Assert.True(editorEmptyState.IsVisible);
            Assert.False(previewEmptyState.IsVisible);
            Assert.False(markdownPreview.IsVisible);

            Click(previewModeButton);
            Assert.Equal(EditorSurfaceMode.Preview, viewModel.EditorMode);
            Assert.True(viewModel.IsPreviewModeSelected);
            Assert.DoesNotContain("active", editModeButton.Classes);
            Assert.Contains("active", previewModeButton.Classes);
            Assert.False(editorEmptyState.IsVisible);
            Assert.True(previewEmptyState.IsVisible);
            Assert.False(markdownPreview.IsVisible);

            Assert.True(aiButton.IsEnabled);
            Assert.Contains("active", aiButton.Classes);
            Assert.True(aiChatButton.IsEnabled);
            Assert.Contains("active", aiChatButton.Classes);
            Click(aiButton);
            Assert.False(viewModel.IsAiPanelExpanded);
            Assert.DoesNotContain("active", aiButton.Classes);

            var darkBackground = BrushColor("ShellBackgroundBrush");
            Assert.Contains("active", themeButton.Classes);
            Click(themeButton);
            Assert.Equal(ShellThemeKind.Light, viewModel.Theme);
            Assert.Equal("深色", ButtonLabel(themeButton));
            Assert.DoesNotContain("active", themeButton.Classes);
            Assert.NotEqual(darkBackground, BrushColor("ShellBackgroundBrush"));
        });
    }

    [AvaloniaFact]
    public async Task MarkdownEditor_UsesSnapshotBindingWithoutRealtimeContentSync()
    {
        var markdown = "# 标题\n\n正文内容";
        var editedMarkdown = "# 新标题\n\n正文内容";
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var secondFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, markdown, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(secondFilePath, markdown, TestContext.Current.CancellationToken);

        try
        {
            var window = new MainWindow();
            window.Show();

            await RunOnUiThreadAsync(async () =>
            {
                var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
                var editor = Find<EditorWorkspace>(window, "EditorWorkspaceControl");
                var editorEmptyState = Find<Grid>(editor, "EditorEmptyState");
                var previewEmptyState = Find<Grid>(editor, "PreviewEmptyState");
                var markdownEditor = Find<NativeMarkdownEditorControl>(editor, "MarkdownEditorControl");
                var textEditor = Find<TextEditor>(markdownEditor, "Editor");
                var markdownPreview = Find<PreviewWebViewControl>(editor, "MarkdownPreviewControl");
                var boldButton = Find<Button>(editor, "BoldButton");
                var editModeButton = Find<Button>(editor, "EditModeButton");
                var previewModeButton = Find<Button>(editor, "PreviewModeButton");

                AssertDeferredShellEntrypointsUnavailable(window);
                Assert.True(editorEmptyState.IsVisible);
                Assert.False(markdownEditor.IsVisible);
                Assert.False(markdownPreview.IsVisible);
                Assert.False(previewEmptyState.IsVisible);
                Assert.False(boldButton.IsEnabled);
                Assert.DoesNotContain(EnumerateControls(editor), control => control is MonacoEditorControl);
                Assert.DoesNotContain("# Hello WeaveDoc!", CollectText(window));

                var opened = await viewModel.DocumentWorkspace.OpenAsync(filePath, TestContext.Current.CancellationToken);

                Assert.True(opened);
                Assert.True(markdownEditor.IsVisible);
                Assert.False(markdownPreview.IsVisible);
                Assert.False(editorEmptyState.IsVisible);
                Assert.True(boldButton.IsEnabled);
                Assert.Equal(markdown, markdownEditor.EditorContent);
                Assert.Equal(markdown, markdownEditor.GetContent());
                Assert.False(markdownEditor.HasUnsyncedContent);
                Assert.Equal(markdown, viewModel.DocumentWorkspace.Content);
                Assert.False(viewModel.DocumentWorkspace.IsDirty);
                Assert.NotEmpty(viewModel.DocumentWorkspace.PreviewHtml);

                markdownEditor.EditorContent = markdown;
                Assert.False(viewModel.DocumentWorkspace.IsDirty);

                textEditor.Text = editedMarkdown;

                Assert.Equal(markdown, markdownEditor.EditorContent);
                Assert.Equal(editedMarkdown, markdownEditor.GetContent());
                Assert.True(markdownEditor.HasUnsyncedContent);
                Assert.Equal(markdown, viewModel.DocumentWorkspace.Content);
                Assert.True(viewModel.DocumentWorkspace.IsDirty);
                Assert.True(viewModel.DocumentWorkspace.CanSave);
                Assert.Contains("<h1 data-line=\"1\">", viewModel.DocumentWorkspace.PreviewHtml);
                Assert.Contains("data-pos=", viewModel.DocumentWorkspace.PreviewHtml);
                Assert.DoesNotContain("新", viewModel.DocumentWorkspace.PreviewHtml);
                AssertDeferredShellEntrypointsUnavailable(window);

                var previewBeforeToolbar = viewModel.DocumentWorkspace.PreviewHtml;
                markdownEditor.SetSelection(2, 3);
                Click(boldButton);
                var formattedMarkdown = "# **新标题**\n\n正文内容";

                Assert.Equal(formattedMarkdown, markdownEditor.GetContent());
                Assert.Equal(markdown, markdownEditor.EditorContent);
                Assert.Equal(markdown, viewModel.DocumentWorkspace.Content);
                Assert.Equal(previewBeforeToolbar, viewModel.DocumentWorkspace.PreviewHtml);
                Assert.True(viewModel.DocumentWorkspace.IsDirty);
                Assert.True(viewModel.DocumentWorkspace.CanSave);
                AssertDeferredShellEntrypointsUnavailable(window);

                Click(previewModeButton);
                await markdownPreview.Activate(false);
                await Task.Delay(50);

                Assert.NotEqual(previewBeforeToolbar, viewModel.DocumentWorkspace.PreviewHtml);
                Assert.Contains("data-pos=\"1-5\">新", viewModel.DocumentWorkspace.PreviewHtml);
                Assert.Contains("<p data-line=\"3\">", viewModel.DocumentWorkspace.PreviewHtml);
                Assert.False(markdownEditor.IsVisible);
                Assert.False(editorEmptyState.IsVisible);
                Assert.False(previewEmptyState.IsVisible);
                Assert.True(markdownPreview.IsVisible);
                Assert.False(boldButton.IsEnabled);
                Assert.Equal(formattedMarkdown, viewModel.DocumentWorkspace.Content);
                Assert.Equal(formattedMarkdown, markdownEditor.EditorContent);
                Assert.Equal(formattedMarkdown, markdownEditor.GetContent());
                Assert.False(markdownEditor.HasUnsyncedContent);
                Assert.True(viewModel.DocumentWorkspace.IsDirty);
                Assert.True(viewModel.DocumentWorkspace.CanSave);
                Assert.Equal(viewModel.DocumentWorkspace.PreviewHtml, markdownPreview.HtmlContent);
                Assert.False(markdownPreview.IsUsingFallback);
                AssertDeferredShellEntrypointsUnavailable(window);

                Click(editModeButton);

                Assert.True(markdownEditor.IsVisible);
                Assert.False(markdownPreview.IsVisible);
                Assert.False(editorEmptyState.IsVisible);
                Assert.Equal(formattedMarkdown, viewModel.DocumentWorkspace.Content);
                Assert.Equal(formattedMarkdown, markdownEditor.EditorContent);
                Assert.Equal(formattedMarkdown, markdownEditor.GetContent());

                var fakeFactory = Assert.IsType<FakeWebViewHostFactory>(WebViewHostFactoryProvider.Current);
                Assert.Same(fakeFactory, markdownPreview.WebViewHostFactory);
                Assert.Contains(fakeFactory.Hosts, host =>
                    host.NavigatedUris.Any(uri => uri.AbsolutePath.EndsWith("/preview-template.html", StringComparison.Ordinal))
                    || host.NavigatedHtml.Any(html =>
                        html.Contains("id='content'", StringComparison.Ordinal)
                        && html.Contains("window.updateContent", StringComparison.Ordinal)));

                var reopened = await viewModel.DocumentWorkspace.OpenAsync(
                    secondFilePath,
                    TestContext.Current.CancellationToken);

                Assert.True(reopened);
                Assert.Equal(Path.GetFileName(secondFilePath), viewModel.DocumentWorkspace.DisplayName);
                Assert.Equal(markdown, viewModel.DocumentWorkspace.Content);
                Assert.Equal(markdown, markdownEditor.EditorContent);
                Assert.Equal(markdown, markdownEditor.GetContent());
                Assert.False(markdownEditor.HasUnsyncedContent);
                Assert.False(viewModel.DocumentWorkspace.IsDirty);
                AssertDeferredShellEntrypointsUnavailable(window);
            });
        }
        finally
        {
            File.Delete(filePath);
            File.Delete(secondFilePath);
        }
    }

    [AvaloniaFact]
    public async Task AiPanelCollapse_HidesRightColumnAndRestoresCurrentSessionWidth()
    {
        var window = new MainWindow
        {
            Width = 1280,
            Height = 820
        };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var workspace = Find<Grid>(window, "ShellWorkspace");
            var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");
            var rightSplitter = Find<GridSplitter>(window, "RightWorkspaceSplitter");
            var aiButton = Find<Button>(window, "AiShellCommandButton");
            var columns = workspace.ColumnDefinitions;

            columns[4].Width = new GridLength(360);
            ArrangeWindow(window);

            Click(aiButton);
            Assert.False(aiPanel.IsVisible);
            Assert.False(rightSplitter.IsVisible);
            Assert.Equal(0, columns[3].Width.Value);
            Assert.Equal(0, columns[4].Width.Value);
            Assert.Equal(0, columns[4].MinWidth);

            Click(aiButton);
            Assert.True(aiPanel.IsVisible);
            Assert.True(rightSplitter.IsVisible);
            Assert.Equal(4, columns[3].Width.Value);
            Assert.Equal(280, columns[4].MinWidth);
            Assert.Equal(360, columns[4].Width.Value, 3);
        });
    }

    [AvaloniaFact]
    public async Task ThemeToggle_RepaintsShellBrushesAndKeepsStatusInSync()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
            var statusBar = Find<ShellStatusBar>(window, "ShellStatusBarControl");
            var statusThemeButton = Find<Button>(statusBar, "ThemeToggleButton");
            var menuThemeButton = Find<Button>(window, "ThemeMenuButton");
            var darkPanel = BrushColor("ShellPanelBrush");

            Assert.Equal(ShellThemeKind.Dark, viewModel.Theme);
            Assert.Equal("浅色", ButtonLabel(statusThemeButton));
            Assert.Equal("浅色", ButtonLabel(menuThemeButton));

            Click(statusThemeButton);
            Assert.Equal(ShellThemeKind.Light, viewModel.Theme);
            Assert.Equal("深色", ButtonLabel(statusThemeButton));
            Assert.Equal("深色", ButtonLabel(menuThemeButton));
            Assert.NotEqual(darkPanel, BrushColor("ShellPanelBrush"));
        });
    }

    [AvaloniaFact]
    public async Task CommandBar_Buttons_RenderIconAndLabel()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var export = Find<Button>(window, "ExportShellDocumentButton");
            var icon = export.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault();
            Assert.NotNull(icon);
            Assert.Contains("shell-icon", icon!.Classes);

            var label = export.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            Assert.NotNull(label);
            Assert.Equal("导出", label!.Text);
        });
    }

    [AvaloniaFact]
    public async Task PendingBusinessEntrypoints_AreDisabled()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");

            AssertDeferredShellEntrypointsUnavailable(window);
            Assert.True(Find<Button>(window, "AiShellCommandButton").IsEnabled);
            Assert.True(Find<Button>(window, "AiChatCommandButton").IsEnabled);
            Assert.True(Find<Button>(window, "AiLiteratureCommandButton").IsEnabled);
            Assert.True(Find<Button>(window, "AiSnapshotCommandButton").IsEnabled);
            Assert.True(Find<Button>(aiPanel, "AiChatTabButton").IsEnabled);
            Assert.True(Find<Button>(aiPanel, "AiLiteratureTabButton").IsEnabled);
            Assert.True(Find<Button>(aiPanel, "AiSnapshotTabButton").IsEnabled);
            Assert.True(Find<Button>(window, "ThemeMenuButton").IsEnabled);
        });
    }

    [AvaloniaFact]
    public async Task AiPanelTabs_SyncBetweenCommandBarAndPanel()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
            var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");
            var commandChatButton = Find<Button>(window, "AiChatCommandButton");
            var commandLiteratureButton = Find<Button>(window, "AiLiteratureCommandButton");
            var commandSnapshotButton = Find<Button>(window, "AiSnapshotCommandButton");
            var panelChatButton = Find<Button>(aiPanel, "AiChatTabButton");
            var panelLiteratureButton = Find<Button>(aiPanel, "AiLiteratureTabButton");
            var panelSnapshotButton = Find<Button>(aiPanel, "AiSnapshotTabButton");

            Assert.Equal(AiPanelTabKind.Chat, viewModel.SelectedAiPanelTab);
            Assert.Contains("active", commandChatButton.Classes);
            Assert.Contains("active", panelChatButton.Classes);
            Assert.DoesNotContain("active", commandLiteratureButton.Classes);
            Assert.DoesNotContain("active", panelLiteratureButton.Classes);

            Click(commandLiteratureButton);
            Assert.Equal(AiPanelTabKind.Literature, viewModel.SelectedAiPanelTab);
            Assert.Contains("active", commandLiteratureButton.Classes);
            Assert.Contains("active", panelLiteratureButton.Classes);
            Assert.DoesNotContain("active", commandChatButton.Classes);
            Assert.DoesNotContain("active", panelChatButton.Classes);

            Click(panelSnapshotButton);
            Assert.Equal(AiPanelTabKind.Snapshot, viewModel.SelectedAiPanelTab);
            Assert.Contains("active", commandSnapshotButton.Classes);
            Assert.Contains("active", panelSnapshotButton.Classes);
            Assert.DoesNotContain("active", commandLiteratureButton.Classes);
            Assert.DoesNotContain("active", panelLiteratureButton.Classes);
        });
    }

    [AvaloniaFact]
    public async Task DocumentPreview_EmptyState_UsesIconBadgeAndGuidance()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var sidebar = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl");
            var paper = sidebar.FindControl<Border>("DocumentPreviewPaper");

            var badge = paper!.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault();
            Assert.NotNull(badge);

            var title = paper.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "尚未打开文档");
            Assert.NotNull(title);
        });
    }

    [AvaloniaFact]
    public async Task RagChat_UsesShellFieldInput()
    {
        var window = new MainWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var panel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");
            var fields = panel.GetVisualDescendants().OfType<TextBox>()
                .Where(t => t.Classes.Contains("shell-field"));
            Assert.NotEmpty(fields);
        });
    }

    [AvaloniaFact]
    public async Task RagChatView_RendersAssistantMarkdownButKeepsUserTextPlain()
    {
        var rag = new RagTabViewModel(new WeaveDoc.Rag.Services.LocalAiService());
        rag.Turns.Add(new ChatTurn("用户", "**不要渲染**", true));
        rag.Turns.Add(new ChatTurn("助手", "## 标题\n\n- 要点\n\n```txt\nhello\n```", false));

        var chatView = new RagChatView
        {
            DataContext = rag
        };
        var window = new Window
        {
            Width = 640,
            Height = 480,
            Content = chatView
        };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ArrangeWindow(window);

            var markdownViews = chatView.GetVisualDescendants().OfType<ChatMarkdownView>()
                .Where(view => view.IsEffectivelyVisible)
                .ToList();
            var textBlocks = chatView.GetVisualDescendants().OfType<TextBlock>().ToList();

            Assert.Single(markdownViews);
            Assert.Equal("## 标题\n\n- 要点\n\n```txt\nhello\n```", markdownViews[0].Markdown);
            Assert.Contains(textBlocks, textBlock => textBlock.Text == "**不要渲染**");
        });
    }

    [AvaloniaFact]
    public async Task AiPanel_ShowsActiveModelProviderBadge()
    {
        var window = new MainWindow(null, null, new WeaveDoc.Rag.Services.LocalAiService());
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = Assert.IsType<AppShellViewModel>(window.DataContext);
            Assert.NotNull(viewModel.RagTabViewModel);
            var rag = viewModel.RagTabViewModel!;
            var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");
            var badgeBorder = Find<Border>(aiPanel, "ProviderBadgeBorder");
            var badgeText = Find<TextBlock>(aiPanel, "ProviderBadgeTextBlock");

            rag.ChatProvider = "cloud";
            rag.CloudModel = "deepseek-chat";

            Assert.True(badgeBorder.IsVisible);
            Assert.Contains("模型:", badgeText.Text);
            Assert.Contains("云端", badgeText.Text);
            Assert.Contains("deepseek-chat", badgeText.Text);

            rag.ChatProvider = "llama_server";

            Assert.Contains("模型:", badgeText.Text);
            Assert.Contains("本地", badgeText.Text);
        });
    }

    [AvaloniaFact]
    public async Task Shell_DoesNotShowRagOrDemoState()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var shellText = CollectText(window);
            var staleDemoTexts = new[]
            {
                "本地 RAG 聊天",
                "准备加载本地模型",
                "正在加载 embedding",
                "模型已就绪",
                "尚未执行检索",
                "本次检索命中",
                "文档转换",
                "模板管理",
                "Attention Is All You Need",
                "论文草稿.md",
                "Qwen2.5",
                "Transformer 架构",
                "会议纪要",
                "第 3 章草稿",
                "参考文献待补充",
                "请帮我总结",
                "用户提问",
                "AI 回复",
                "暂无导入文档",
                "删除选中文档",
                "本地设置"
            };

            Assert.All(staleDemoTexts, text => Assert.DoesNotContain(text, shellText));
        });
    }

    [AvaloniaFact]
    public async Task StatusBar_RendersStatusPill()
    {
        var window = new MainWindow();
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var bar = Find<ShellStatusBar>(window, "ShellStatusBarControl");
            var pills = bar.GetVisualDescendants().OfType<Border>()
                .Where(b => b.Classes.Contains("status-pill"));
            Assert.NotEmpty(pills);
        });
    }

    private static T Find<T>(Control? root, string name) where T : Control
    {
        Assert.NotNull(root);
        var control = root!.FindControl<T>(name);
        Assert.NotNull(control);
        return control!;
    }

    private static string ButtonLabel(Button button)
    {
        var label = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        return label?.Text ?? button.Content?.ToString() ?? string.Empty;
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static async Task RunOnUiThreadAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        await completion.Task;
    }

    private static void ArrangeWindow(Window window)
    {
        window.Measure(new Avalonia.Size(window.Width, window.Height));
        window.Arrange(new Avalonia.Rect(0, 0, window.Width, window.Height));
    }

    private static void DragSplitter(Window window, Control splitter, double deltaX)
    {
        var center = splitter.TranslatePoint(
            new Avalonia.Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2),
            window);
        Assert.NotNull(center);

        var start = center.Value;
        var end = new Avalonia.Point(start.X + deltaX, start.Y);

        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
    }

    private static void AssertWorkspaceColumnMinimums(Window window, Grid workspace)
    {
        var columns = workspace.ColumnDefinitions;

        Assert.Equal(300, columns[0].MinWidth);
        Assert.Equal(400, columns[2].MinWidth);
        Assert.Equal(280, columns[4].MinWidth);
        AssertVisibleSize(Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl"), 300, 1);
        AssertVisibleSize(Find<EditorWorkspace>(window, "EditorWorkspaceControl"), 400, 1);
        AssertVisibleSize(Find<AiAssistantPanel>(window, "AiAssistantPanelControl"), 280, 1);
    }

    private static void AssertWorkspaceLayoutIsStable(Window window, Grid workspace)
    {
        var sidebar = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl");
        var leftSplitter = Find<GridSplitter>(window, "LeftWorkspaceSplitter");
        var editor = Find<EditorWorkspace>(window, "EditorWorkspaceControl");
        var rightSplitter = Find<GridSplitter>(window, "RightWorkspaceSplitter");
        var aiPanel = Find<AiAssistantPanel>(window, "AiAssistantPanelControl");

        AssertVisibleSize(leftSplitter, 4, 1);
        AssertVisibleSize(rightSplitter, 4, 1);

        var totalColumnWidth = sidebar.Bounds.Width
            + leftSplitter.Bounds.Width
            + editor.Bounds.Width
            + rightSplitter.Bounds.Width
            + aiPanel.Bounds.Width;
        Assert.True(
            totalColumnWidth >= workspace.Bounds.Width - 1
                && totalColumnWidth <= workspace.Bounds.Width + leftSplitter.Bounds.Width + rightSplitter.Bounds.Width,
            $"workspace width was {workspace.Bounds.Width}, column sum was {totalColumnWidth}");
    }

    private static void AssertVisibleSize(Control control, double minimumWidth, double minimumHeight)
    {
        Assert.True(control.IsVisible);
        Assert.True(control.Bounds.Width >= minimumWidth, $"{control.Name} width was {control.Bounds.Width}");
        Assert.True(control.Bounds.Height >= minimumHeight, $"{control.Name} height was {control.Bounds.Height}");
    }

    private static void AssertDeferredShellEntrypointsUnavailable(Window window)
    {
        var sidebar = Find<WorkspaceSidebar>(window, "WorkspaceSidebarControl");
        var editor = Find<EditorWorkspace>(window, "EditorWorkspaceControl");

        Assert.Null(window.FindControl<Control>("FileMenuButton"));
        Assert.Null(window.FindControl<Control>("EditMenuButton"));
        Assert.Null(window.FindControl<Control>("ViewMenuButton"));
        Assert.Null(window.FindControl<Control>("AiMenuButton"));
        Assert.Null(window.FindControl<Control>("LiteratureMenuButton"));
        Assert.Null(window.FindControl<Control>("ExportMenuButton"));
        Assert.Null(window.FindControl<Control>("HelpMenuButton"));
        Assert.Null(window.FindControl<Control>("ConvertButton"));
        Assert.Null(window.FindControl<Control>("TemplateGrid"));
        Assert.DoesNotContain("# Hello WeaveDoc!", CollectText(window));

        // 工作线 4: the AI panel chat input (清空/发送/输入框) moved into RagChatView and is no
        // longer a "deferred" entrypoint, so they are dropped from this disabled list.
        var disabledButtons = new[]
        {
            // NewShellDocumentButton was enabled in 工作线 2 (task 2.3) — no longer deferred
            // OpenShellDocumentButton was enabled in 工作线 0 (task 0.2) — no longer deferred
            // OpenDocumentButton / SaveDocumentButton removed from EditorWorkspace toolbar in 工作线 2 (task 2.4)
            // SetupShellCommandButton was enabled in 工作线 3 — opens SettingsDialog, no longer deferred
            Find<Button>(sidebar, "DocumentPreviousPageButton"),
            Find<Button>(sidebar, "DocumentPreviousPageButton"),
            Find<Button>(sidebar, "DocumentNextPageButton"),
            Find<Button>(sidebar, "DocumentZoomOutButton"),
            Find<Button>(sidebar, "DocumentZoomInButton"),
            Find<Button>(sidebar, "DocumentPreviewTabButton"),
            Find<Button>(sidebar, "DocumentPreviewCloseTabButton"),
            Find<Button>(sidebar, "DocumentPreviewAddTabButton"),
            Find<Button>(editor, "ExportDocumentButton")
        };

        Assert.All(disabledButtons, button => Assert.False(button.IsEnabled));
        Assert.False(Find<TextBox>(window, "ShellSearchBox").IsEnabled);
    }

    private static void AssertBrushResource(string key)
    {
        var resource = Avalonia.Application.Current!.Resources[key];
        Assert.IsAssignableFrom<IBrush>(resource);
    }

    private static Color BrushColor(string key)
    {
        var resource = Avalonia.Application.Current!.Resources[key];
        var brush = Assert.IsType<SolidColorBrush>(resource);
        return brush.Color;
    }

    private static string CollectText(Control root)
    {
        var text = EnumerateControls(root)
            .Select(control => control switch
            {
                Button button => button.Content?.ToString(),
                TextBlock textBlock => textBlock.Text,
                TextBox textBox => textBox.Text,
                _ => null
            })
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join('\n', text);
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        yield return root;

        foreach (var child in root.GetLogicalChildren().OfType<Control>())
        {
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }
}
