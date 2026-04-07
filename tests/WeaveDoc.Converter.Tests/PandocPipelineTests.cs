using System.Text.Json;
using Xunit;
using WeaveDoc.Converter.Pandoc;
using WeaveDoc.Converter.Afd.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WeaveDoc.Converter.Tests;

public class PandocPipelineTests
{
    private static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, ".gitignore")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("无法找到解决方案根目录");
    }

    private static PandocPipeline CreatePipeline()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc-3.9.0.2", "pandoc.exe");
        return new PandocPipeline(pandocPath);
    }

    private static string CreateTempMarkdown(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pandoc-test-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task ToDocxAsync_InvalidInput_ThrowsException()
    {
        var pipeline = CreatePipeline();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.docx");

        await Assert.ThrowsAsync<Exception>(() =>
            pipeline.ToDocxAsync("/nonexistent/file.md", outputPath));
    }

    [Fact]
    public async Task ToDocxAsync_WithInput_ProducesDocx()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 测试标题\n\n这是正文段落。\n");
        var docxPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.docx");

        try
        {
            await pipeline.ToDocxAsync(mdPath, docxPath);

            Assert.True(File.Exists(docxPath));
            Assert.True(new FileInfo(docxPath).Length > 0);
        }
        finally
        {
            File.Delete(mdPath);
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public async Task ToAstJsonAsync_ReturnsValidJson()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 标题\n\n正文内容\n");

        try
        {
            var json = await pipeline.ToAstJsonAsync(mdPath);

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("blocks", out _));
        }
        finally
        {
            File.Delete(mdPath);
        }
    }

    private static AfdTemplate CreateTestTemplate() => new()
    {
        Meta = new AfdMeta { TemplateName = "测试模板" },
        Defaults = new AfdDefaults
        {
            FontFamily = "宋体",
            FontSize = 12,
            LineSpacing = 1.5,
            PageSize = new AfdPageSize { Width = 210, Height = 297 },
            Margins = new AfdMargins { Top = 25, Bottom = 25, Left = 30, Right = 30 }
        },
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["heading1"] = new()
            {
                DisplayName = "标题 1",
                FontFamily = "黑体",
                FontSize = 16,
                Bold = true,
                Alignment = "center",
                SpaceBefore = 24,
                SpaceAfter = 18,
                LineSpacing = 1.5
            },
            ["body"] = new()
            {
                DisplayName = "正文",
                FontFamily = "宋体",
                FontSize = 12,
                FirstLineIndent = 24,
                LineSpacing = 1.5
            }
        }
    };

    [Fact]
    public void ReferenceDocBuilder_Build_CreatesValidDocx()
    {
        var template = CreateTestTemplate();
        var outputPath = Path.Combine(Path.GetTempPath(), $"ref-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(outputPath, template);

            Assert.True(File.Exists(outputPath));

            using var doc = WordprocessingDocument.Open(outputPath, false);
            var stylesPart = doc.MainDocumentPart?.StyleDefinitionsPart;
            Assert.NotNull(stylesPart);
            Assert.NotNull(stylesPart.Styles);

            // 验证 Heading1 样式存在且字体为黑体
            var heading1 = stylesPart.Styles.Elements<Style>()
                .FirstOrDefault(s => s.StyleId == "Heading1");
            Assert.NotNull(heading1);

            var rPr = heading1.Elements<StyleRunProperties>().First();
            var fonts = rPr.Elements<RunFonts>().First();
            Assert.Equal("黑体", fonts.EastAsia?.Value);

            var fontSize = rPr.Elements<FontSize>().First();
            Assert.Equal("32", fontSize.Val?.Value); // 16pt = 32 half-points
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
