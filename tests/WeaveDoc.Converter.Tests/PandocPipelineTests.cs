using System.Text.Json;
using Xunit;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;
using WeaveDoc.Converter.Afd.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;

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
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
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

    [Fact]
    public void OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"style-test-{Guid.NewGuid():N}.docx");

        try
        {
            // 先用 ReferenceDocBuilder 生成含 Heading1 的 docx
            ReferenceDocBuilder.Build(docxPath, template);

            // 在已生成的 docx 上添加内容段落来测试修正
            using (var doc = WordprocessingDocument.Open(docxPath, true))
            {
                var body = doc.MainDocumentPart!.Document.Body!;
                var p = new Paragraph();
                p.AppendChild(new ParagraphProperties(
                    new ParagraphStyleId { Val = "Heading1" }));
                p.AppendChild(new Run(new Text("测试标题")));
                body.AppendChild(p);

                var bodyP = new Paragraph();
                bodyP.AppendChild(new ParagraphProperties(
                    new ParagraphStyleId { Val = "Normal" }));
                bodyP.AppendChild(new Run(new Text("正文内容")));
                body.AppendChild(bodyP);

                doc.MainDocumentPart.Document.Save();
            }

            // 执行修正
            OpenXmlStyleCorrector.ApplyAfdStyles(docxPath, template);

            // 验证 Heading1 段落的字体和字号
            using (var doc = WordprocessingDocument.Open(docxPath, false))
            {
                var body = doc.MainDocumentPart!.Document.Body!;
                var heading = body.Elements<Paragraph>()
                    .First(p => p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "Heading1");

                var run = heading.Elements<Run>().First();
                var rPr = run.RunProperties;
                Assert.NotNull(rPr);

                var fonts = rPr.Elements<RunFonts>().First();
                Assert.Equal("黑体", fonts.EastAsia?.Value);

                var fontSize = rPr.Elements<FontSize>().First();
                Assert.Equal("32", fontSize.Val?.Value); // 16pt = 32 half-points
            }
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public void OpenXmlStyleCorrector_ApplyPageSettings_SetsDimensions()
    {
        var docxPath = Path.Combine(Path.GetTempPath(), $"page-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, CreateTestTemplate());

            var defaults = new AfdDefaults
            {
                PageSize = new AfdPageSize { Width = 210, Height = 297 },
                Margins = new AfdMargins { Top = 25, Bottom = 25, Left = 30, Right = 30 }
            };

            OpenXmlStyleCorrector.ApplyPageSettings(docxPath, defaults);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var sectPr = doc.MainDocumentPart!.Document.Body!.Elements<SectionProperties>().First();
            var pgSz = sectPr.Elements<PageSize>().First();

            // 210mm * 567 = 119070, 297mm * 567 = 168399
            Assert.Equal(119070u, pgSz.Width?.Value);
            Assert.Equal(168399u, pgSz.Height?.Value);

            var pgMar = sectPr.Elements<PageMargin>().First();
            // 25mm * 567 = 14175, 30mm * 567 = 17010
            Assert.Equal(14175, pgMar.Top?.Value);
            Assert.Equal((uint)17010, pgMar.Left?.Value);
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public async Task FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var templatePath = Path.Combine(root,
            "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "default-thesis.json");

        // 用真实 AfdParser 解析模板
        var parser = new Afd.AfdParser();
        var template = parser.Parse(templatePath);

        // ConfigManager 还是 stub，直接测试管线各组件串联
        var pipeline = new PandocPipeline(pandocPath);

        var mdContent = "# 测试论文标题\n\n这是正文段落，用于测试。\n\n## 二级标题\n\n更多内容。\n";
        var mdPath = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid():N}.md");
        var outputDir = Path.Combine(Path.GetTempPath(), $"e2e-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(mdPath, mdContent);

        try
        {
            // 因为 ConfigManager 还是 stub，这里先测试 Pandoc 直接调用路径
            var refDocPath = Path.Combine(outputDir, "reference.docx");
            ReferenceDocBuilder.Build(refDocPath, template);

            var rawDocxPath = Path.Combine(outputDir, "raw.docx");
            await pipeline.ToDocxAsync(mdPath, rawDocxPath, refDocPath);

            OpenXmlStyleCorrector.ApplyAfdStyles(rawDocxPath, template);
            OpenXmlStyleCorrector.ApplyPageSettings(rawDocxPath, template.Defaults);

            Assert.True(File.Exists(rawDocxPath));

            // 验证输出可以被 OpenXML 正确打开
            using var doc = WordprocessingDocument.Open(rawDocxPath, false);
            Assert.NotNull(doc.MainDocumentPart);
            Assert.NotNull(doc.MainDocumentPart.Document.Body);
        }
        finally
        {
            File.Delete(mdPath);
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_Docx()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-test-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new Config.ConfigManager(dbPath);
            var template = CreateTestTemplate();
            await configManager.SaveTemplateAsync("test-tpl", template);

            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试标题\n\n正文内容。\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "docx");

                Assert.True(result.Success, $"转换失败: {result.ErrorMessage}");
                Assert.True(File.Exists(result.OutputPath), "输出文件不存在");

                using var doc = WordprocessingDocument.Open(result.OutputPath, false);
                Assert.NotNull(doc.MainDocumentPart);
                Assert.NotNull(doc.MainDocumentPart.Document.Body);
            }
            finally
            {
                File.Delete(mdPath);
                if (File.Exists(Path.ChangeExtension(mdPath, "docx")))
                    File.Delete(Path.ChangeExtension(mdPath, "docx"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_MissingTemplate()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-missing-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new ConfigManager(dbPath);
            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-missing-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "nonexistent-tpl", "docx");

                Assert.False(result.Success);
                Assert.Contains("nonexistent-tpl", result.ErrorMessage);
            }
            finally
            {
                File.Delete(mdPath);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_UnsupportedFormat()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-unsup-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new ConfigManager(dbPath);
            var template = CreateTestTemplate();
            await configManager.SaveTemplateAsync("test-tpl", template);

            var pipeline = new PandocPipeline(pandocPath);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-unsup-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# 测试\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "html");

                Assert.False(result.Success);
                Assert.Contains("html", result.ErrorMessage);
            }
            finally
            {
                File.Delete(mdPath);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void OpenXmlStyleCorrector_ApplyHeaderFooter()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"hf-test-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, template);

            var headerFooter = new AfdHeaderFooter
            {
                Header = new AfdHeaderContent
                {
                    Text = "测试页眉",
                    FontFamily = "宋体",
                    FontSize = 9,
                    Alignment = "center"
                },
                Footer = new AfdFooterContent
                {
                    PageNumbering = true,
                    Alignment = "center",
                    StartPage = 1
                }
            };

            OpenXmlStyleCorrector.ApplyHeaderFooter(docxPath, headerFooter);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var mainPart = doc.MainDocumentPart!;
            var sectPr = mainPart.Document.Body!.Elements<SectionProperties>().Last();

            // 验证 HeaderPart 存在且包含指定文本
            var headerRefs = sectPr.Elements<HeaderReference>().ToList();
            Assert.Single(headerRefs);

            var headerId = headerRefs[0].Id!.Value!;
            var headerPart = (HeaderPart)mainPart.GetPartById(headerId);
            Assert.NotNull(headerPart.Header);

            var headerPara = headerPart.Header.Elements<Paragraph>().First();
            var headerRun = headerPara.Elements<Run>().First();
            Assert.Equal("测试页眉", headerRun.GetFirstChild<Text>()?.Text);

            // 验证字体
            var rPr = headerRun.RunProperties;
            Assert.NotNull(rPr);
            var fonts = rPr.Elements<RunFonts>().First();
            Assert.Equal("宋体", fonts.EastAsia?.Value);
            var fontSize = rPr.Elements<FontSize>().First();
            Assert.Equal("18", fontSize.Val?.Value); // 9pt = 18 half-points

            // 验证 FooterPart 存在且包含 PAGE 字段
            var footerRefs = sectPr.Elements<FooterReference>().ToList();
            Assert.Single(footerRefs);

            var footerId = footerRefs[0].Id!.Value!;
            var footerPart = (FooterPart)mainPart.GetPartById(footerId);
            Assert.NotNull(footerPart.Footer);

            var footerParas = footerPart.Footer.Elements<Paragraph>().ToList();
            Assert.NotEmpty(footerParas);
            var footerPara = footerParas.First();
            var fieldCodes = footerPara.Elements<Run>()
                .SelectMany(r => r.Elements<FieldCode>())
                .ToList();
            Assert.Contains(fieldCodes, fc => fc.Text?.Contains("PAGE") == true);
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public void OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"hf-start-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, template);

            var headerFooter = new AfdHeaderFooter
            {
                Footer = new AfdFooterContent
                {
                    PageNumbering = true,
                    Alignment = "center",
                    StartPage = 3
                }
            };

            OpenXmlStyleCorrector.ApplyHeaderFooter(docxPath, headerFooter);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var sectPr = doc.MainDocumentPart!.Document.Body!.Elements<SectionProperties>().Last();

            var pgNumType = sectPr.Elements<PageNumberType>().FirstOrDefault();
            Assert.NotNull(pgNumType);
            Assert.Equal(3, pgNumType.Start?.Value);
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }
}
