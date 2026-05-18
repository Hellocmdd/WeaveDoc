using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;
using WeaveDoc.Converter.Ui.Views;
using Xunit;

namespace WeaveDoc.Converter.Ui.Tests;

public class ConvertTabTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _configManager;

    public ConvertTabTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"convert-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _configManager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [AvaloniaFact]
    public async Task ConvertTab_LoadTemplates_PopulatesComboBox()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        // Seed templates
        await _configManager.EnsureSeedTemplatesAsync();

        // Create engine and set services
        var pipeline = new PandocPipeline();
        var engine = new DocumentConversionEngine(pipeline, new SyncfusionPdfConverter(), _configManager);
        tab.SetServices(_configManager, engine);

        // Wait for async template loading
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var combo = tab.FindControl<ComboBox>("TemplateCombo");
            Assert.NotNull(combo);
            Assert.NotNull(combo.ItemsSource);
            var items = combo.ItemsSource!.Cast<AfdMeta>().ToList();
            Assert.True(items.Count >= 3, $"Expected at least 3 seed templates, got {items.Count}");
            Assert.Equal(0, combo.SelectedIndex);
        });
    }

    [AvaloniaFact]
    public async Task ConvertTab_ConvertWithoutMd_ShowsErrorStatus()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        // Set up services (templates will load but no MD file selected)
        await _configManager.EnsureSeedTemplatesAsync();
        var pipeline = new PandocPipeline();
        var engine = new DocumentConversionEngine(pipeline, new SyncfusionPdfConverter(), _configManager);
        tab.SetServices(_configManager, engine);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var convertButton = tab.FindControl<Button>("ConvertButton");
            Assert.NotNull(convertButton);

            // Raise click event without selecting an MD file
            convertButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var statusLabel = tab.FindControl<TextBlock>("StatusLabel");
            Assert.NotNull(statusLabel);
            Assert.Contains("请选择", statusLabel.Text);
        });
    }

    [AvaloniaFact]
    public async Task ConvertTab_FormatToggleButtons_Exist()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var formatDocxBtn = tab.FindControl<Button>("FormatDocxBtn");
            var formatPdfBtn = tab.FindControl<Button>("FormatPdfBtn");

            Assert.NotNull(formatDocxBtn);
            Assert.NotNull(formatPdfBtn);
            Assert.Equal("DOCX", formatDocxBtn.Content);
            Assert.Equal("PDF", formatPdfBtn.Content);
        });
    }

    [AvaloniaFact]
    public async Task ConvertTab_PdfLayoutSelector_DefaultsSingleAndOnlyShowsForPdf()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var layoutPanel = tab.FindControl<Avalonia.Controls.Control>("PdfLayoutPanel");
            var singleButton = tab.FindControl<Button>("PdfSingleColumnBtn");
            var doubleButton = tab.FindControl<Button>("PdfTwoColumnBtn");
            var pdfButton = tab.FindControl<Button>("FormatPdfBtn");
            var docxButton = tab.FindControl<Button>("FormatDocxBtn");

            Assert.NotNull(layoutPanel);
            Assert.NotNull(singleButton);
            Assert.NotNull(doubleButton);
            Assert.False(layoutPanel!.IsVisible);
            Assert.Equal("单列", singleButton!.Content);
            Assert.Equal("双列", doubleButton!.Content);
            Assert.Equal(FontWeight.SemiBold, singleButton.FontWeight);

            pdfButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(layoutPanel.IsVisible);

            docxButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(layoutPanel.IsVisible);
        });
    }

    [AvaloniaFact]
    public async Task ConvertTab_ConvertPdfAfterSelectingTwoColumn_PassesTwoColumnLayout()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await _configManager.EnsureSeedTemplatesAsync();
        var pipeline = new PandocPipeline();
        var converter = new LayoutInspectingPdfConverter();
        var engine = new DocumentConversionEngine(pipeline, converter, _configManager);
        tab.SetServices(_configManager, engine);

        var waitedForTemplates = 0;
        while (waitedForTemplates < 10000)
        {
            var hasTemplate = await Dispatcher.UIThread.InvokeAsync(() => tab.FindControl<ComboBox>("TemplateCombo")!.SelectedItem != null);
            if (hasTemplate)
                break;

            await Task.Delay(200);
            waitedForTemplates += 200;
        }

        var mdPath = Path.Combine(_tempDir, "paper.md");
        await File.WriteAllTextAsync(mdPath, "# 论文标题\n\n作者 A\n\n正文第一段\n");

        var outputDir = Path.Combine(_tempDir, "pdf-output");
        Directory.CreateDirectory(outputDir);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            tab.FindControl<TextBox>("MdPathBox")!.Text = mdPath;
            tab.FindControl<TextBox>("OutputDirBox")!.Text = outputDir;

            tab.FindControl<Button>("FormatPdfBtn")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tab.FindControl<Button>("PdfTwoColumnBtn")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tab.FindControl<Button>("ConvertButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        var waited = 0;
        while (!converter.WasCalled && waited < 10000)
        {
            await Task.Delay(200);
            waited += 200;
        }

        Assert.True(converter.WasCalled, "PDF converter was not called.");
        Assert.True(converter.SawTwoColumnFinalSection, converter.Failure);
    }

    [AvaloniaFact]
    public async Task ConvertTab_ConvertDocx_EndToEnd()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        // Seed templates
        await _configManager.EnsureSeedTemplatesAsync();
        var pipeline = new PandocPipeline();
        var engine = new DocumentConversionEngine(pipeline, new SyncfusionPdfConverter(), _configManager);
        tab.SetServices(_configManager, engine);

        // Get the first template name for expected output path
        var templates = await _configManager.ListTemplatesAsync();
        Assert.NotEmpty(templates);
        var templateName = templates[0].TemplateName;

        // Create a test Markdown file
        var mdPath = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(mdPath, "# 测试标题\n\n正文内容\n");

        // Create output directory
        var outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(outputDir);

        // Set UI state and trigger conversion on the UI thread
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var mdBox = tab.FindControl<TextBox>("MdPathBox");
            var outputDirBox = tab.FindControl<TextBox>("OutputDirBox");
            Assert.NotNull(mdBox);
            Assert.NotNull(outputDirBox);
            mdBox.Text = mdPath;
            outputDirBox.Text = outputDir;

            var convertButton = tab.FindControl<Button>("ConvertButton");
            Assert.NotNull(convertButton);
            convertButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        // Wait for the async void handler to complete and produce output
        var outputFile = Path.Combine(outputDir, $"test-{templateName}.docx");
        var waited = 0;
        while (!File.Exists(outputFile) && waited < 10000)
        {
            await Task.Delay(200);
            waited += 200;
        }

        Assert.True(File.Exists(outputFile), $"DOCX output not found after {waited}ms: {outputFile}");
        Assert.True(new FileInfo(outputFile).Length > 0, "DOCX output is empty");
    }

    [AvaloniaFact]
    public async Task ConvertTab_ConvertDocx_UsesCustomOutputFileName()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await _configManager.EnsureSeedTemplatesAsync();
        var pipeline = new PandocPipeline();
        var engine = new DocumentConversionEngine(pipeline, new SyncfusionPdfConverter(), _configManager);
        tab.SetServices(_configManager, engine);

        var mdPath = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(mdPath, "# 测试标题\n\n正文内容\n");

        var outputDir = Path.Combine(_tempDir, "custom-output");
        Directory.CreateDirectory(outputDir);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var mdBox = tab.FindControl<TextBox>("MdPathBox");
            var outputDirBox = tab.FindControl<TextBox>("OutputDirBox");
            var outputFileNameBox = tab.FindControl<TextBox>("OutputFileNameBox");
            Assert.NotNull(mdBox);
            Assert.NotNull(outputDirBox);
            Assert.NotNull(outputFileNameBox);

            mdBox.Text = mdPath;
            outputDirBox.Text = outputDir;
            outputFileNameBox.Text = "自定义导出名";

            var convertButton = tab.FindControl<Button>("ConvertButton");
            Assert.NotNull(convertButton);
            convertButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });

        var outputFile = Path.Combine(outputDir, "自定义导出名.docx");
        var waited = 0;
        while (!File.Exists(outputFile) && waited < 10000)
        {
            await Task.Delay(200);
            waited += 200;
        }

        Assert.True(File.Exists(outputFile), $"Custom DOCX output not found after {waited}ms: {outputFile}");
        Assert.True(new FileInfo(outputFile).Length > 0, "Custom DOCX output is empty");
    }

    private sealed class LayoutInspectingPdfConverter : IPdfConverter
    {
        public string Name => "layout-inspecting";
        public bool WasCalled { get; private set; }
        public bool SawTwoColumnFinalSection { get; private set; }
        public string Failure { get; private set; } = "";

        public void ConvertToPdf(string docxPath, string pdfPath)
        {
            WasCalled = true;
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var finalSection = doc.MainDocumentPart!.Document.Body!.Elements<SectionProperties>().Last();
            SawTwoColumnFinalSection = finalSection.GetFirstChild<Columns>()?.ColumnCount?.Value == 2;
            if (!SawTwoColumnFinalSection)
                Failure = "最终 section 没有收到双列设置，说明 UI 没有把 TwoColumn 传入转换引擎。";

            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
            File.WriteAllText(pdfPath, "%PDF-1.7 layout-inspecting");
        }
    }
}
