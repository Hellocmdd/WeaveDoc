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

    [Fact]
    public async Task GetTemplate_NotExist_ReturnsNull()
    {
        var result = await _manager.GetTemplateAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task ListTemplates_ReturnsAll()
    {
        await _manager.SaveTemplateAsync("tpl-a", CreateTestTemplate("模板A"));
        await _manager.SaveTemplateAsync("tpl-b", CreateTestTemplate("模板B"));

        var list = await _manager.ListTemplatesAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, m => m.TemplateName == "模板A");
        Assert.Contains(list, m => m.TemplateName == "模板B");
    }

    [Fact]
    public async Task DeleteTemplate_RemovesFromDbAndFile()
    {
        await _manager.SaveTemplateAsync("to-delete", CreateTestTemplate());
        var before = await _manager.GetTemplateAsync("to-delete");
        Assert.NotNull(before);

        await _manager.DeleteTemplateAsync("to-delete");

        var after = await _manager.GetTemplateAsync("to-delete");
        Assert.Null(after);
    }

    [Fact]
    public async Task SaveTemplate_OverwritesExisting()
    {
        await _manager.SaveTemplateAsync("overwrite", CreateTestTemplate("V1"));
        await _manager.SaveTemplateAsync("overwrite", CreateTestTemplate("V2"));

        var result = await _manager.GetTemplateAsync("overwrite");
        Assert.NotNull(result);
        Assert.Equal("V2", result.Meta.TemplateName);
    }

    [Fact]
    public async Task EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates()
    {
        await _manager.EnsureSeedTemplatesAsync();

        var courseReport = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(courseReport);
        Assert.Equal("课程报告", courseReport.Meta.TemplateName);

        var labReport = await _manager.GetTemplateAsync("lab-report");
        Assert.NotNull(labReport);
        Assert.Equal("实验报告", labReport.Meta.TemplateName);

        var thesis = await _manager.GetTemplateAsync("default-thesis");
        Assert.NotNull(thesis);
        Assert.Equal("默认学术论文", thesis.Meta.TemplateName);
    }

    [Fact]
    public async Task EnsureSeedTemplatesAsync_SkipsExistingTemplates()
    {
        // 先保存一个修改版的 course-report（Description 不同）
        var modified = CreateTestTemplate("课程报告");
        modified = modified with
        {
            Meta = modified.Meta with { Description = "用户自定义版" }
        };
        await _manager.SaveTemplateAsync("course-report", modified);

        await _manager.EnsureSeedTemplatesAsync();

        var result = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(result);
        // 验证是用户自定义版，不是内置版（内置版 Description = "高校课程报告通用模板"）
        Assert.Equal("用户自定义版", result.Meta.Description);
    }

    [Fact]
    public async Task EnsureSeedTemplatesAsync_Idempotent()
    {
        await _manager.EnsureSeedTemplatesAsync();
        await _manager.EnsureSeedTemplatesAsync();

        var result = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(result);
        Assert.Equal("课程报告", result.Meta.TemplateName);
    }
}
