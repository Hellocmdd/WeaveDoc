using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    public async Task ConvertTab_FormatRadioButtons_DefaultIsDocx()
    {
        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var formatDocx = tab.FindControl<RadioButton>("FormatDocx");
            var formatPdf = tab.FindControl<RadioButton>("FormatPdf");

            Assert.NotNull(formatDocx);
            Assert.NotNull(formatPdf);
            Assert.True(formatDocx.IsChecked == true, "FormatDocx should be checked by default");
            Assert.True(formatPdf.IsChecked == false, "FormatPdf should not be checked by default");
        });
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
}
