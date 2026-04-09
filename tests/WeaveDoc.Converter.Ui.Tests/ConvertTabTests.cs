using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
        var engine = new DocumentConversionEngine(pipeline, _configManager);
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
        var engine = new DocumentConversionEngine(pipeline, _configManager);
        tab.SetServices(_configManager, engine);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var convertButton = tab.FindControl<Button>("ConvertButton");
            Assert.NotNull(convertButton);

            // Raise click event without selecting an MD file
            convertButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

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
}
