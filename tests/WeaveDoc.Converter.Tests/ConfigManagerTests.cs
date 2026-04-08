using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using Xunit;

namespace WeaveDoc.Converter.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _manager;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _manager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static AfdTemplate CreateTestTemplate(string name = "测试模板") => new()
    {
        Meta = new AfdMeta
        {
            TemplateName = name,
            Version = "1.0.0",
            Author = "Test",
            Description = "测试用模板"
        },
        Defaults = new AfdDefaults
        {
            FontFamily = "宋体",
            FontSize = 12,
            LineSpacing = 1.5
        },
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["heading1"] = new()
            {
                DisplayName = "标题 1",
                FontFamily = "黑体",
                FontSize = 16,
                Bold = true
            }
        }
    };

    [Fact]
    public async Task SaveAndGetTemplate_RoundTrips()
    {
        var template = CreateTestTemplate();

        await _manager.SaveTemplateAsync("test-tpl", template);
        var result = await _manager.GetTemplateAsync("test-tpl");

        Assert.NotNull(result);
        Assert.Equal("测试模板", result.Meta.TemplateName);
        Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
    }
}
