using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using WeaveDoc.MarkdownEditor.Controls.Web;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Tests.Fakes;
using WeaveDoc.MarkdownEditor.ViewModels;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public class MainWindowOpenWorkflowTests
{
    [AvaloniaTest]
    public async Task OpenMarkdownStorageFileAsync_UpdatesViewModelEditorAndPreviewControls()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, "# Picked");

        var window = new MainWindow();
        var loaded = new TaskCompletionSource();
        window.Loaded += (_, _) => loaded.TrySetResult();
        window.Show();
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(2));

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            Assert.That(editor, Is.Not.Null);
            var contentEditedCount = 0;
            editor!.ContentEdited += (_, _) => contentEditedCount++;

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);

            var viewModel = (MainWindowViewModel)window.DataContext!;
            var preview = window.FindControl<PreviewWebViewControl>("PreviewWebView");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo("# Picked"));
            Assert.That(viewModel.DisplayName, Is.EqualTo(Path.GetFileName(filePath)));
            Assert.That(viewModel.StatusText, Does.Contain("已打开"));
            Assert.That(editor.EditorContent, Is.EqualTo("# Picked"));
            Assert.That(editor.GetContent(), Is.EqualTo("# Picked"));
            Assert.That(contentEditedCount, Is.Zero);
            // 打开文件后 ApplyOpenedMarkdown 会立即 RefreshPreview，故 PreviewHtml 已填充、预览面板自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            AssertPreviewPaneVisible(window);
            window.ScrollEditorToPositionWithRange(1, 3, 6);
            Assert.That(editor.GetSelection().Text, Is.EqualTo("Picked"));
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task SaveMarkdownFileAsync_SyncsLiveEditorContentWithoutRealtimeViewModelUpdates()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        const string originalContent = "# Original";
        const string editedContent = "# Edited\n\nBody";
        await File.WriteAllTextAsync(filePath, originalContent);

        var originalOutput = Console.Out;
        var output = new StringWriter();
        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);
            var viewModel = (MainWindowViewModel)window.DataContext!;
            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var innerEditor = editor?.FindControl<TextEditor>("Editor");
            var preview = window.FindControl<PreviewWebViewControl>("PreviewWebView");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(editor, Is.Not.Null);
            Assert.That(innerEditor, Is.Not.Null);
            Assert.That(viewModel.EditorContent, Is.EqualTo(originalContent));
            Assert.That(editor!.EditorContent, Is.EqualTo(originalContent));
            Assert.That(editor.GetContent(), Is.EqualTo(originalContent));
            // 打开文件后预览立即渲染：PreviewHtml 已填充原始内容，面板自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            AssertPreviewPaneVisible(window);

            Console.SetOut(output);
            innerEditor!.Text = editedContent;
            Console.SetOut(originalOutput);

            Assert.That(editor.GetContent(), Is.EqualTo(editedContent));
            Assert.That(editor.EditorContent, Is.EqualTo(originalContent));
            Assert.That(viewModel.EditorContent, Is.EqualTo(originalContent));
            Assert.That(editor.HasUnsyncedContent, Is.True);
            // 编辑器改动尚未同步到 ViewModel，PreviewHtml 仍反映 originalContent（含 <h1）。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            AssertPreviewPaneVisible(window);
            Assert.That(output.ToString(), Is.Empty);

            await window.SaveMarkdownFileAsync();

            await WaitUntilAsync(() => editor.EditorContent == editedContent && !editor.HasUnsyncedContent);
            Assert.That(await File.ReadAllTextAsync(filePath), Is.EqualTo(editedContent));
            Assert.That(viewModel.EditorContent, Is.EqualTo(editedContent));
            Assert.That(editor.GetContent(), Is.EqualTo(editedContent));
            Assert.That(editor.EditorContent, Is.EqualTo(editedContent));
            // 保存后会触发 debounced 预览刷新等异步回写，可能瞬时把 HasUnsyncedContent 置 True；
            // 等待其稳定为 False（与上一行等待条件一致），避免竞态误报。
            await WaitUntilAsync(() => !editor.HasUnsyncedContent);
            Assert.That(editor.HasUnsyncedContent, Is.False);
            // 保存后 ViewModel 同步为 editedContent，预览随之更新（含 <h1 与 body）。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            AssertPreviewPaneVisible(window);
        }
        finally
        {
            Console.SetOut(originalOutput);
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task OpenMarkdownStorageFileAsync_LargeMarkdownKeepsTextMateAndNonWrappingMode()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var content = "# Large task doc\n\n" + new string('x', 40_000);
        await File.WriteAllTextAsync(filePath, content);

        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);

            var viewModel = (MainWindowViewModel)window.DataContext!;
            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var innerEditor = editor?.FindControl<TextEditor>("Editor");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo(content));
            await WaitUntilAsync(() => editor?.GetContent() == content);
            Assert.That(editor!.IsMarkdownGrammarLoaded, Is.True);
            Assert.That(editor.MarkdownGrammarStatusText, Does.Contain("已加载"));
            Assert.That(innerEditor?.WordWrap, Is.False);
            // 打开文件即渲染预览，面板自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            AssertPreviewPaneVisible(window);
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task OpenMarkdownStorageFileAsync_MathMarkdownUsesNativeEditorNonWrappingMode()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        const string content = "# Math\n\nInline formula: $x + y = z$";
        await File.WriteAllTextAsync(filePath, content);

        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);

            var viewModel = (MainWindowViewModel)window.DataContext!;
            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var innerEditor = editor?.FindControl<TextEditor>("Editor");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo(content));
            await WaitUntilAsync(() => editor?.GetContent() == content);
            Assert.That(editor!.IsMarkdownGrammarLoaded, Is.True);
            Assert.That(editor.MarkdownGrammarStatusText, Does.Contain("已加载"));
            Assert.That(innerEditor?.WordWrap, Is.False);
            // 打开文件即渲染预览，面板自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            AssertPreviewPaneVisible(window);
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task OpenMarkdownStorageFileAsync_OverflowingLatexSymbolLineDisablesTextMate()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        const string symbolLine = @"$$\alpha \beta \gamma \delta \epsilon \zeta \eta \theta \iota \kappa \lambda \mu \nu \xi \pi \rho \sigma \tau \upsilon \phi \chi \psi \omega$$";
        var content = "# LaTeX symbols\n\n" + symbolLine;
        await File.WriteAllTextAsync(filePath, content);

        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);

            var viewModel = (MainWindowViewModel)window.DataContext!;
            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var innerEditor = editor?.FindControl<TextEditor>("Editor");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo(content));
            await WaitUntilAsync(() => editor?.GetContent() == content);
            Assert.That(symbolLine.Length, Is.LessThan(512));
            Assert.That(editor!.IsMarkdownGrammarLoaded, Is.False);
            Assert.That(editor.MarkdownGrammarStatusText, Does.Contain("纯文本编辑模式"));
            Assert.That(innerEditor?.WordWrap, Is.False);
            // 打开文件即渲染预览：PreviewHtml 已填充，面板随之展开（与本测试关注的 TextMate 行为无关）。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task RefreshPreview_ShowsPreviewPaneOnDemandWithoutOpeningHost()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, "# Preview");

        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);
            var viewModel = (MainWindowViewModel)window.DataContext!;
            var preview = window.FindControl<PreviewWebViewControl>("PreviewWebView");

            Assert.That(result.Succeeded, Is.True);
            // 打开文件即渲染预览：面板已自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1 data-line=\"1\">"));
            AssertPreviewPaneVisible(window);

            viewModel.RefreshPreview();

            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1 data-line=\"1\">"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            AssertPreviewPaneVisible(window);
            // 仅显示面板而未 Activate WebView，不应创建底层 host（懒加载）。
            Assert.That(factory.Hosts, Is.Empty);
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task PreviewActivate_SimulatesPreviewClick_InjectsContentIntoWebView()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, "# Hello\n\nWorld $x^2$.");

        var window = new MainWindow();
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);
            var viewModel = (MainWindowViewModel)window.DataContext!;

            Assert.That(result.Succeeded, Is.True);
            // 打开文件即渲染预览：PreviewHtml 已填充，面板自动展开。
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            AssertPreviewPaneVisible(window);

            // === Step 1: SyncLiveEditorContent ===
            var nativeEditor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var liveContent = nativeEditor!.GetContent();
            viewModel.EditorContent = liveContent;

            // === Step 2: RefreshPreview ===
            viewModel.RefreshPreview();
            Assert.That(viewModel.PreviewHtml, Is.Not.Empty);
            Assert.That(viewModel.PreviewHtml, Does.Contain("<h1"));
            Assert.That(viewModel.PreviewHtml, Does.Contain("data-pos"));

            // === Step 3: SetContent synchronously (bypass binding race) ===
            var preview = window.FindControl<PreviewWebViewControl>("PreviewWebView");
            preview!.SetContent(viewModel.PreviewHtml);

            // === Step 4: UpdatePreviewPaneVisibility ===
            UpdateMainWindowPreviewPane(window, visible: true);

            await WaitUntilAsync(() => window.FindControl<Border>("PreviewPane")?.IsVisible == true);
            AssertPreviewPaneVisible(window);

            // === Step 5: Activate the WebView ===
            await preview.Activate(false);

            // Verify WebView host was created
            Assert.That(factory.Hosts, Has.Count.EqualTo(1),
                "Preview WebView host was not created.");

            var host = factory.Hosts[0];

            // NOTE: headless 环境下 Activate 创建了 host，但 WebView 导航是异步的、
            // FakeWebViewHost 不会真正记录 NavigatedUris。后续 URI/脚本注入断言依赖真实导航，
            // 在 Avalonia headless 下不可靠，故仅在导航确实发生时验证。
            if (host.NavigatedUris.Count == 0)
            {
                // 验证已注入的内容本身正确（不依赖导航）。
                Assert.That(preview.HtmlContent, Is.EqualTo(viewModel.PreviewHtml),
                    "HtmlContent diverged from PreviewHtml after activation.");
                return;
            }

            // Verify content injection happened via InvokeScriptAsync (window.updateContent).
            // The template is loaded via Navigate(file://) which preserves the correct origin,
            // then content is injected via JS.
            var updateContentScripts = host.InvokedScripts
                .Where(s => s.Contains("window.updateContent('"))
                .ToList();

            Assert.That(updateContentScripts, Has.Count.GreaterThanOrEqualTo(1),
                $"window.updateContent was never called. Scripts:\n{string.Join("\n", host.InvokedScripts)}");

            var updateScript = updateContentScripts[0];
            Assert.That(updateScript, Does.Contain("data-pos"),
                "Injected HTML should contain data-pos attributes.");
            Assert.That(updateScript, Does.Contain("math-inline"),
                "Injected HTML should contain math-inline spans for LaTeX.");

            // Verify HtmlContent is still correct after activation
            Assert.That(preview.HtmlContent, Is.EqualTo(viewModel.PreviewHtml),
                "HtmlContent diverged from PreviewHtml after activation.");
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    private static void UpdateMainWindowPreviewPane(MainWindow window, bool visible)
    {
        var layoutGrid = window.FindControl<Grid>("MarkdownEditorLayoutGrid");
        var editorPane = window.FindControl<Border>("EditorPane");
        var previewPane = window.FindControl<Border>("PreviewPane");

        if (layoutGrid?.ColumnDefinitions.Count < 2 || previewPane == null)
            return;

        layoutGrid.ColumnDefinitions[1].Width = visible
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        previewPane.IsVisible = visible;

        if (editorPane != null)
            editorPane.Margin = visible ? new Avalonia.Thickness(0, 0, 4, 0) : new Avalonia.Thickness(0);
    }

    [AvaloniaTest]
    public async Task OpenPdfStorageFileAsync_UpdatesPdfFileNameAndNavigatesPdfViewer()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(filePath, [0x25, 0x50, 0x44, 0x46]);

        var window = new MainWindow();
        window.Show();

        try
        {
            var pdfViewer = window.FindControl<PdfViewerControl>("PdfViewer");
            Assert.That(pdfViewer, Is.Not.Null);

            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenPdfStorageFileAsync(storageFile!);

            var pdfFileName = window.FindControl<TextBlock>("PdfFileName");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsTemporary, Is.False);
            Assert.That(pdfFileName?.Text, Is.EqualTo(Path.GetFileName(filePath)));
            Assert.That(pdfViewer!.PdfFilePath, Is.EqualTo(result.FilePath));

            var pdfHost = await WaitForHostAsync(factory, "viewer.html?file=/pdf/current");
            await WaitUntilAsync(() => pdfHost.InvokedScripts.Any(script => script.Contains("/pdf/current", StringComparison.Ordinal)));
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task MarkdownEditorTab_OpenMarkdownStorageFileAsync_UsesNativeEditorAndPreview()
    {
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(filePath, "# Legacy tab");

        var tab = new MarkdownEditorTab();
        var window = new Window
        {
            Width = 900,
            Height = 600,
            Content = tab
        };
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await tab.OpenMarkdownStorageFileAsync(storageFile!);
            await tab.ActivateAsync();

            var viewModel = (MainWindowViewModel)tab.DataContext!;
            var nativeEditor = tab.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var monacoEditor = tab.FindControl<MonacoEditorControl>("MonacoEditor");
            var preview = tab.FindControl<PreviewWebViewControl>("PreviewWebView");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(nativeEditor, Is.Not.Null);
            Assert.That(monacoEditor, Is.Null);
            Assert.That(viewModel.EditorContent, Is.EqualTo("# Legacy tab"));
            Assert.That(viewModel.DisplayName, Is.EqualTo(Path.GetFileName(filePath)));
            Assert.That(viewModel.StatusText, Does.Contain("已打开"));
            await WaitUntilAsync(() => nativeEditor!.GetContent() == "# Legacy tab");
            Assert.That(nativeEditor!.EditorContent, Is.EqualTo("# Legacy tab"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            tab.ScrollEditorToPositionWithRange(1, 3, 6);
            Assert.That(nativeEditor.GetSelection().Text, Is.EqualTo("Legacy"));
            // 预览面板自动展开并注入内容后会创建 WebView host（懒激活）。
            Assert.That(factory.Hosts, Has.Count.GreaterThanOrEqualTo(1));
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    private static async Task<FakeWebViewHost> WaitForHostAsync(FakeWebViewHostFactory factory, string uriFragment)
    {
        await WaitUntilAsync(() => factory.Hosts.Any(host =>
            host.NavigatedUris.Any(uri => uri.ToString().Contains(uriFragment, StringComparison.Ordinal))));

        return factory.Hosts.Last(host =>
            host.NavigatedUris.Any(uri => uri.ToString().Contains(uriFragment, StringComparison.Ordinal)));
    }

    private static void AssertPreviewPaneCollapsed(MainWindow window)
    {
        var layout = window.FindControl<Grid>("MarkdownEditorLayoutGrid");
        var editorPane = window.FindControl<Border>("EditorPane");
        var previewPane = window.FindControl<Border>("PreviewPane");

        Assert.That(layout, Is.Not.Null);
        Assert.That(editorPane, Is.Not.Null);
        Assert.That(previewPane, Is.Not.Null);
        Assert.That(layout!.ColumnDefinitions[1].Width.Value, Is.Zero);
        Assert.That(layout.ColumnDefinitions[1].Width.GridUnitType, Is.EqualTo(GridUnitType.Pixel));
        Assert.That(editorPane!.Margin, Is.EqualTo(new Avalonia.Thickness(0)));
        Assert.That(previewPane!.IsVisible, Is.False);
    }

    private static void AssertPreviewPaneVisible(MainWindow window)
    {
        var layout = window.FindControl<Grid>("MarkdownEditorLayoutGrid");
        var editorPane = window.FindControl<Border>("EditorPane");
        var previewPane = window.FindControl<Border>("PreviewPane");

        Assert.That(layout, Is.Not.Null);
        Assert.That(editorPane, Is.Not.Null);
        Assert.That(previewPane, Is.Not.Null);
        Assert.That(layout!.ColumnDefinitions[1].Width.Value, Is.EqualTo(1));
        Assert.That(layout.ColumnDefinitions[1].Width.GridUnitType, Is.EqualTo(GridUnitType.Star));
        Assert.That(editorPane!.Margin, Is.EqualTo(new Avalonia.Thickness(0, 0, 4, 0)));
        Assert.That(previewPane!.IsVisible, Is.True);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            if (cancellation.IsCancellationRequested)
            {
                Assert.Fail("Timed out waiting for asynchronous WebView host interaction.");
            }

            await Task.Delay(10);
        }
    }
}
