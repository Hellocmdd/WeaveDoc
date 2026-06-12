using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Controls.Web;
using WeaveDoc.MarkdownEditor.Tests.Fakes;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Tests;

[TestFixture]
public sealed class StandaloneLatexPerformanceProbeTests
{
    [AvaloniaTest]
    public async Task Probe_StandaloneMainWindowAfterPreviewPaneCollapse()
    {
        var filePath = ResolveMarkdownFixture("test-latex.md");
        var factory = new FakeWebViewHostFactory();
        WebViewHostFactoryProvider.Current = factory;

        var window = new MainWindow
        {
            Width = 1000,
            Height = 700
        };
        window.Show();

        try
        {
            var storageFile = await window.StorageProvider.TryGetFileFromPathAsync(filePath);
            Assert.That(storageFile, Is.Not.Null);

            var open = await MeasureAsync(() => window.OpenMarkdownStorageFileAsync(storageFile!), iterations: 1);
            var nativeEditor = window.FindControl<NativeMarkdownEditorControl>("NativeEditor")
                ?? throw new InvalidOperationException("NativeEditor not found.");
            var textEditor = nativeEditor.FindControl<TextEditor>("Editor")
                ?? throw new InvalidOperationException("Inner TextEditor not found.");
            var layout = window.FindControl<Grid>("MarkdownEditorLayoutGrid")
                ?? throw new InvalidOperationException("MarkdownEditorLayoutGrid not found.");
            var previewPane = window.FindControl<Border>("PreviewPane")
                ?? throw new InvalidOperationException("PreviewPane not found.");

            var scroll = Measure(() =>
            {
                nativeEditor.RevealLine(1);
                nativeEditor.RevealLine(90);
            }, iterations: 10);
            var input = Measure(() =>
            {
                textEditor.Document.Insert(textEditor.Document.TextLength, "x");
            }, iterations: 20);
            var enter = Measure(() =>
            {
                textEditor.Document.Insert(textEditor.Document.TextLength, "\n");
            }, iterations: 20);

            Assert.Fail(
                $"[PERF-PROBE] Open={open.TotalMilliseconds:F3}ms " +
                $"Scrollx10={scroll.TotalMilliseconds:F3}ms " +
                $"Inputx20={input.TotalMilliseconds:F3}ms " +
                $"Enterx20={enter.TotalMilliseconds:F3}ms " +
                $"WordWrap={textEditor.WordWrap} TextMate={nativeEditor.IsMarkdownGrammarLoaded} " +
                $"PreviewHosts={factory.Hosts.Count} " +
                $"PreviewColumn={layout.ColumnDefinitions[1].Width} " +
                $"PreviewVisible={previewPane.IsVisible}");
        }
        finally
        {
            window.Close();
        }
    }

    private static TimeSpan Measure(Action action, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static async Task<TimeSpan> MeasureAsync(Func<Task> action, int iterations)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static string ResolveMarkdownFixture(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "test_doc", "markdown", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate markdown fixture {fileName}.");
    }
}
