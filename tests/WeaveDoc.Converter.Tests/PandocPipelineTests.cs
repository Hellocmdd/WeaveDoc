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
        var tectonicDir = Path.Combine(root, "tools", "tectonic");
        return new PandocPipeline(pandocPath, tectonicDir);
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

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var paragraphs = body.Descendants<Paragraph>().ToList();
            Assert.Contains(paragraphs, p => p.InnerText.Contains("测试标题"));
            Assert.Contains(paragraphs, p => p.InnerText.Contains("正文段落"));
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

            // 验证 Normal 样式存在且使用模板默认字体
            var normal = stylesPart.Styles!.Elements<Style>()
                .FirstOrDefault(s => s.StyleId == "Normal");
            Assert.NotNull(normal);
            var normalRPr = normal.Elements<StyleRunProperties>().First();
            var normalFonts = normalRPr.Elements<RunFonts>().First();
            Assert.Equal("宋体", normalFonts.EastAsia?.Value);
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

            // 210mm × (1440/25.4) ≈ 11905, 297mm × (1440/25.4) ≈ 16837
            Assert.Equal(11905u, pgSz.Width?.Value);
            Assert.Equal(16837u, pgSz.Height?.Value);

            var pgMar = sectPr.Elements<PageMargin>().First();
            // 25mm × (1440/25.4) ≈ 1417, 30mm × (1440/25.4) ≈ 1700
            Assert.Equal(1417, pgMar.Top?.Value);
            Assert.Equal((uint)1700, pgMar.Left?.Value);
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

            using var doc = WordprocessingDocument.Open(rawDocxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;

            // 验证 Heading1 段落样式（default-thesis.json: 黑体、16pt）
            var heading = body.Descendants<Paragraph>()
                .First(p => p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "Heading1");
            Assert.Contains("测试论文标题", heading.InnerText);
            var run = heading.Elements<Run>().First();
            var rPr = run.RunProperties;
            Assert.NotNull(rPr);
            Assert.Equal("黑体", rPr.Elements<RunFonts>().First().EastAsia?.Value);
            Assert.Equal("32", rPr.Elements<FontSize>().First().Val?.Value); // 16pt = 32 half-points

            // 验证页面尺寸（A4: 210×297mm → twips）
            var sectPr = body.Elements<SectionProperties>().First();
            var pgSz = sectPr.Elements<PageSize>().First();
            Assert.Equal(11905u, pgSz.Width?.Value);
            Assert.Equal(16837u, pgSz.Height?.Value);
        }
        finally
        {
            File.Delete(mdPath);
            try { Directory.Delete(outputDir, true); } catch { }
        }
    }

    /// <summary>
    /// 参数化完整管线测试：验证每个 AFD 模板都能走通 Parse → RefDoc → Pandoc → StyleCorrector → PageSettings → HeaderFooter。
    /// </summary>
    [Theory]
    [InlineData("course-report.json", "课程报告", 16, 25, 25, "课程报告", 10.5)]
    [InlineData("lab-report.json", "实验报告", 18, 25.4, 31.7, "实验报告", 9)]
    public async Task FullPipeline_NewTemplate_ProducesValidDocx(
        string templateFile, string expectedTemplateName,
        int heading1FontSize, double marginTopMm, double marginLeftMm,
        string expectedHeaderText, double expectedHeaderFontSize)
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var templatePath = Path.Combine(root,
            "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", templateFile);

        // 1. 解析模板
        var parser = new Afd.AfdParser();
        var template = parser.Parse(templatePath);
        Assert.Equal(expectedTemplateName, template.Meta.TemplateName);

        var pipeline = new PandocPipeline(pandocPath);
        var mdContent = "# 测试标题\n\n正文段落。\n\n## 二级标题\n\n更多内容。\n";
        var mdPath = Path.Combine(Path.GetTempPath(), $"tpl-e2e-{Guid.NewGuid():N}.md");
        var outputDir = Path.Combine(Path.GetTempPath(), $"tpl-e2e-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(mdPath, mdContent);

        try
        {
            // 2. 构建参考文档
            var refDocPath = Path.Combine(outputDir, "reference.docx");
            ReferenceDocBuilder.Build(refDocPath, template);

            // 3. Pandoc 转换
            var rawDocxPath = Path.Combine(outputDir, "raw.docx");
            await pipeline.ToDocxAsync(mdPath, rawDocxPath, refDocPath);

            // 4. 样式修正 + 页面设置 + 页眉页脚
            OpenXmlStyleCorrector.ApplyAfdStyles(rawDocxPath, template);
            OpenXmlStyleCorrector.ApplyPageSettings(rawDocxPath, template.Defaults);
            if (template.HeaderFooter != null)
                OpenXmlStyleCorrector.ApplyHeaderFooter(rawDocxPath, template.HeaderFooter);

            // 5. 验证 DOCX
            Assert.True(File.Exists(rawDocxPath));

            using var doc = WordprocessingDocument.Open(rawDocxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;

            // 验证 Heading1 字体和字号
            var heading = body.Descendants<Paragraph>()
                .First(p => p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "Heading1");
            var run = heading.Elements<Run>().First();
            var rPr = run.RunProperties;
            Assert.NotNull(rPr);
            Assert.Equal("黑体", rPr.Elements<RunFonts>().First().EastAsia?.Value);
            Assert.Equal((heading1FontSize * 2).ToString(), rPr.Elements<FontSize>().First().Val?.Value);

            // 验证页面尺寸（A4: 210×297mm）
            var sectPr = body.Elements<SectionProperties>().First();
            var pgSz = sectPr.Elements<PageSize>().First();
            Assert.Equal(11905u, pgSz.Width?.Value);
            Assert.Equal(16837u, pgSz.Height?.Value);

            // 验证页边距
            var pgMar = sectPr.Elements<PageMargin>().First();
            Assert.Equal((int)(marginTopMm * 1440.0 / 25.4), pgMar.Top?.Value);
            Assert.Equal((uint)(marginLeftMm * 1440.0 / 25.4), pgMar.Left?.Value);

            // 验证页眉
            var headerRefs = sectPr.Elements<HeaderReference>().ToList();
            Assert.Single(headerRefs);
            var headerId = headerRefs[0].Id!.Value!;
            var headerPart = (HeaderPart)doc.MainDocumentPart.GetPartById(headerId);
            var headerPara = headerPart.Header.Elements<Paragraph>().First();
            var headerRun = headerPara.Elements<Run>().First();
            Assert.Equal(expectedHeaderText, headerRun.GetFirstChild<Text>()?.Text);
            var headerFontSize = headerRun.RunProperties!.Elements<FontSize>().First();
            Assert.Equal(((int)(expectedHeaderFontSize * 2)).ToString(), headerFontSize.Val?.Value);

            // 验证页脚包含 PAGE 字段
            var footerRefs = sectPr.Elements<FooterReference>().ToList();
            Assert.Single(footerRefs);
            var footerId = footerRefs[0].Id!.Value!;
            var footerPart = (FooterPart)doc.MainDocumentPart.GetPartById(footerId);
            var fieldCodes = footerPart.Footer.Elements<Paragraph>()
                .SelectMany(p => p.Elements<Run>())
                .SelectMany(r => r.Elements<FieldCode>());
            Assert.Contains(fieldCodes, fc => fc.Text?.Contains("PAGE") == true);
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
                var body = doc.MainDocumentPart!.Document.Body!;

                // 验证 Heading1 段落样式（CreateTestTemplate: 黑体、16pt）
                var heading = body.Descendants<Paragraph>()
                    .First(p => p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "Heading1");
                var headingRun = heading.Elements<Run>().First();
                var headingRPr = headingRun.RunProperties;
                Assert.NotNull(headingRPr);
                Assert.Equal("黑体", headingRPr.Elements<RunFonts>().First().EastAsia?.Value);
                Assert.Equal("32", headingRPr.Elements<FontSize>().First().Val?.Value); // 16pt = 32 half-points
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

    [Fact]
    public void OpenXmlStyleCorrector_ApplyAfdStyles_StylesTableCellParagraphs()
    {
        var template = CreateTestTemplate();
        var docxPath = Path.Combine(Path.GetTempPath(), $"table-style-{Guid.NewGuid():N}.docx");

        try
        {
            ReferenceDocBuilder.Build(docxPath, template);

            // 创建含表格的 DOCX，表格单元格内放置带 Heading1 样式的段落
            using (var doc = WordprocessingDocument.Open(docxPath, true))
            {
                var body = doc.MainDocumentPart!.Document.Body!;
                var table = new Table(
                    new TableProperties(new TableStyle { Val = "TableGrid" }),
                    new TableRow(
                        new TableCell(
                            new Paragraph(
                                new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                                new Run(new Text("表格内标题"))))));
                body.AppendChild(table);
                doc.MainDocumentPart.Document.Save();
            }

            OpenXmlStyleCorrector.ApplyAfdStyles(docxPath, template);

            using (var doc = WordprocessingDocument.Open(docxPath, false))
            {
                var body = doc.MainDocumentPart!.Document.Body!;
                var tablePara = body.Descendants<Paragraph>()
                    .First(p => p.InnerText == "表格内标题");

                var run = tablePara.Elements<Run>().First();
                var rPr = run.RunProperties;
                Assert.NotNull(rPr);

                // heading1 对应黑体
                var fonts = rPr.Elements<RunFonts>().First();
                Assert.Equal("黑体", fonts.EastAsia?.Value);
            }
        }
        finally
        {
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public async Task FromDocxToPdfAsync_WithDocxInput_ProducesPdf()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# PDF测试\n\n从DOCX生成PDF。\n");
        var docxPath = Path.Combine(Path.GetTempPath(), $"pdf-src-{Guid.NewGuid():N}.docx");
        var pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-out-{Guid.NewGuid():N}.pdf");

        try
        {
            await pipeline.ToDocxAsync(mdPath, docxPath);
            Assert.True(File.Exists(docxPath));

            await pipeline.FromDocxToPdfAsync(docxPath, pdfPath,
                mainFont: "SimSun", cjkMainFont: "SimSun", monoFont: "SimSun");

            AssertIsPdf(pdfPath);
        }
        finally
        {
            File.Delete(mdPath);
            if (File.Exists(docxPath)) File.Delete(docxPath);
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task DocumentConversionEngine_ConvertAsync_Pdf()
    {
        var root = FindSolutionRoot();
        var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
        var tectonicDir = Path.Combine(root, "tools", "tectonic");
        var dbPath = Path.Combine(Path.GetTempPath(), $"dce-pdf-{Guid.NewGuid():N}.db");

        try
        {
            var configManager = new ConfigManager(dbPath);
            var template = CreateTestTemplate();
            await configManager.SaveTemplateAsync("test-tpl", template);

            var pipeline = new PandocPipeline(pandocPath, tectonicDir);
            var engine = new DocumentConversionEngine(pipeline, configManager);

            var mdPath = Path.Combine(Path.GetTempPath(), $"dce-pdf-{Guid.NewGuid():N}.md");
            File.WriteAllText(mdPath, "# PDF测试标题\n\n这是PDF正文内容。\n");

            try
            {
                var result = await engine.ConvertAsync(mdPath, "test-tpl", "pdf");

                Assert.True(result.Success, $"PDF 转换失败: {result.ErrorMessage}");
                Assert.True(File.Exists(result.OutputPath), "PDF 输出文件不存在");
                Assert.EndsWith(".pdf", result.OutputPath);
                AssertIsPdf(result.OutputPath);
            }
            finally
            {
                File.Delete(mdPath);
                if (File.Exists(Path.ChangeExtension(mdPath, "pdf")))
                    File.Delete(Path.ChangeExtension(mdPath, "pdf"));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ToDocxAsync_Blockquote_AppliesBlockquoteStyle()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 标题\n\n> 这是引用块内容\n");
        var docxPath = Path.Combine(Path.GetTempPath(), $"bq-{Guid.NewGuid():N}.docx");

        try
        {
            await pipeline.ToDocxAsync(mdPath, docxPath);
            Assert.True(File.Exists(docxPath));

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;

            // Lua filter 将 BlockQuote 包裹在 custom-style="Blockquote" 的 Div 中，
            // Pandoc DOCX writer 会输出 pStyle="Blockquote" 的段落
            var paragraphs = body.Descendants<Paragraph>().ToList();
            var bqParagraph = paragraphs.FirstOrDefault(p =>
                p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "Blockquote");
            Assert.NotNull(bqParagraph);
            Assert.Contains("引用块内容", bqParagraph.InnerText);
        }
        finally
        {
            File.Delete(mdPath);
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    [Fact]
    public async Task ToDocxAsync_CodeBlock_AppliesCodeBlockStyle()
    {
        var pipeline = CreatePipeline();
        var mdPath = CreateTempMarkdown("# 标题\n\n```\nvar x = 1;\n```\n");
        var docxPath = Path.Combine(Path.GetTempPath(), $"cb-{Guid.NewGuid():N}.docx");

        try
        {
            await pipeline.ToDocxAsync(mdPath, docxPath);
            Assert.True(File.Exists(docxPath));

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;

            // Lua filter 将 CodeBlock 转为 Para 并包裹在 custom-style="CodeBlock" 的 Div 中，
            // Pandoc DOCX writer 输出 pStyle="CodeBlock" 的段落
            var paragraphs = body.Descendants<Paragraph>().ToList();
            var cbParagraph = paragraphs.FirstOrDefault(p =>
                p.GetFirstChild<ParagraphProperties>()?.ParagraphStyleId?.Val?.Value == "CodeBlock");
            Assert.NotNull(cbParagraph);
            Assert.Contains("var x = 1;", cbParagraph.InnerText);
        }
        finally
        {
            File.Delete(mdPath);
            if (File.Exists(docxPath)) File.Delete(docxPath);
        }
    }

    private static void AssertIsPdf(string path)
    {
        Assert.True(File.Exists(path), $"PDF 文件不存在: {path}");
        Assert.True(new FileInfo(path).Length > 1024, "PDF 文件过小，可能无效");

        using var stream = File.OpenRead(path);
        var header = new byte[5];
        stream.Read(header, 0, 5);
        var magic = System.Text.Encoding.ASCII.GetString(header);
        Assert.Equal("%PDF-", magic);
    }
}
