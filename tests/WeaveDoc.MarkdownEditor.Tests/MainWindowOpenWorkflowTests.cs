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
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var result = await window.OpenMarkdownStorageFileAsync(storageFile!);

            var viewModel = (MainWindowViewModel)window.DataContext!;
            var editor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor");
            var preview = window.FindControl<PreviewWebViewControl>("PreviewWebView");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(viewModel.EditorContent, Is.EqualTo("# Picked"));
            Assert.That(viewModel.DisplayName, Is.EqualTo(Path.GetFileName(filePath)));
            Assert.That(viewModel.StatusText, Does.Contain("已打开"));
            Assert.That(editor?.EditorContent, Is.EqualTo("# Picked"));
            Assert.That(editor?.GetContent(), Is.EqualTo("# Picked"));
            Assert.That(preview?.HtmlContent, Is.EqualTo(viewModel.PreviewHtml));
            window.ScrollEditorToPositionWithRange(1, 3, 6);
            Assert.That(editor?.GetSelection().Text, Is.EqualTo("Picked"));
            Assert.That(factory.Hosts, Is.Empty);
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
    }

    [AvaloniaTest]
    public async Task OpenMarkdownStorageFileAsync_LargeMarkdownUsesNativeEditorPerformanceMode()
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
            Assert.That(editor!.IsMarkdownGrammarLoaded, Is.False);
            Assert.That(editor.MarkdownGrammarStatusText, Does.Contain("大 Markdown 文件"));
            Assert.That(innerEditor?.WordWrap, Is.False);
            Assert.That(viewModel.PreviewHtml, Is.Empty);
            Assert.That(factory.Hosts, Is.Empty);
        }
        finally
        {
            window.Close();
            File.Delete(filePath);
        }
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
            Assert.That(factory.Hosts, Is.Empty);
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
