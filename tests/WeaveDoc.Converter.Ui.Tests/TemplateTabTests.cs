using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Ui.Views;
using Xunit;

namespace WeaveDoc.Converter.Ui.Tests;

public class TemplateTabTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _configManager;

    public TemplateTabTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ui-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _configManager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [AvaloniaFact]
    public async Task TemplateTab_LoadTemplates_DisplaysInGrid()
    {
        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        // 保存测试模板
        await _configManager.SaveTemplateAsync("test-tpl", new AfdTemplate
        {
            Meta = new AfdMeta { TemplateName = "测试", Version = "1.0", Author = "Test", Description = "desc" },
            Defaults = new AfdDefaults { FontFamily = "宋体", FontSize = 12, LineSpacing = 1.5 },
            Styles = new Dictionary<string, AfdStyleDefinition>
            {
                ["body"] = new() { DisplayName = "正文", FontSize = 12 }
            }
        });

        // 绑定 ConfigManager 并加载
        tab.SetConfigManager(_configManager);

        // 等待异步加载完成
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var grid = tab.FindControl<DataGrid>("TemplateGrid");
            Assert.NotNull(grid);
            Assert.NotNull(grid.ItemsSource);
            var items = grid.ItemsSource!.Cast<AfdMeta>().ToList();
            Assert.Single(items);
            Assert.Equal("测试", items[0].TemplateName);
        });
    }

    [AvaloniaFact]
    public async Task TemplateTab_SeedButton_SeedTemplates()
    {
        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        tab.SetConfigManager(_configManager);

        // 执行种子模板
        await _configManager.EnsureSeedTemplatesAsync();
        await tab.LoadTemplatesAsync();

        var grid = tab.FindControl<DataGrid>("TemplateGrid");
        Assert.NotNull(grid);
        var items = grid.ItemsSource!.Cast<AfdMeta>().ToList();
        Assert.True(items.Count >= 3, $"期望至少 3 个种子模板，实际 {items.Count}");
        Assert.Contains(items, i => i.TemplateName == "课程报告");
        Assert.Contains(items, i => i.TemplateName == "实验报告");
        Assert.Contains(items, i => i.TemplateName == "默认学术论文");
    }

    [AvaloniaFact]
    public async Task TemplateTab_StatusBar_ShowsTemplateCount()
    {
        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        tab.SetConfigManager(_configManager);

        await tab.LoadTemplatesAsync();

        var status = tab.FindControl<TextBlock>("StatusBar");
        Assert.NotNull(status);
        Assert.Contains("0", status.Text);
    }
}
