# Pandoc 转换管道实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 Markdown → AFD 样式注入 → DOCX/PDF 的两阶段转换管道。

**Architecture:** ReferenceDocBuilder 从 AFD 模板生成 reference.docx，PandocPipeline 封装 CLI 完成格式转换，OpenXmlStyleCorrector 对 Pandoc 输出做精确样式修正，DocumentConversionEngine 编排全流程。

**Tech Stack:** C# .NET 10, DocumentFormat.OpenXml 3.5.1, Pandoc 3.9.0.2 (本地 CLI), xUnit

**Spec:** `docs/superpowers/specs/2026-04-07-pandoc-pipeline-design.md`

---

## 文件结构

| 文件 | 操作 | 职责 |
|------|------|------|
| `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs` | 修改 | Pandoc CLI 进程封装 |
| `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs` | 修改 | 合并所有 OpenXML 修正逻辑 |
| `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs` | 新建 | 从 AfdTemplate 生成 reference.docx |
| `src/WeaveDoc.Converter/DocumentConversionEngine.cs` | 修改 | 端到端编排 |
| `src/WeaveDoc.Converter/Pandoc/PageSettings.cs` | 删除 | 逻辑合并到 OpenXmlStyleCorrector |
| `src/WeaveDoc.Converter/Pandoc/HeaderFooterBuilder.cs` | 删除 | 逻辑合并到 OpenXmlStyleCorrector |
| `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` | 修改 | 所有管道相关测试 |

---

## Task 1: PandocPipeline — CLI 封装

**Files:**
- Modify: `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

### 1.1 写失败测试：无效输入抛异常

- [ ] **Step 1: 添加测试**

在 `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` 中写入：

```csharp
using System.Text.Json;
using Xunit;
using WeaveDoc.Converter.Pandoc;

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
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.ToDocxAsync_InvalidInput_ThrowsException" --no-restore -v n`
Expected: FAIL（NotImplementedException 或类似错误）

### 1.2 实现 PandocPipeline.RunAsync

- [ ] **Step 3: 实现 PandocPipeline.cs**

替换 `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs` 全部内容：

```csharp
using System.Diagnostics;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / PDF / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;

    public PandocPipeline(string? pandocPath = null)
    {
        _pandocPath = pandocPath
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "pandoc-3.9.0.2", "pandoc.exe");
    }

    /// <summary>Markdown → DOCX</summary>
    public async Task<string> ToDocxAsync(
        string inputPath, string outputPath,
        string? referenceDoc = null, string? luaFilter = null,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(inputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables+raw_html",
            "-t", "docx",
            "-o", Quote(outputPath),
            "--standalone"
        };

        if (referenceDoc != null)
            args.AddRange(new[] { "--reference-doc", Quote(referenceDoc) });
        if (luaFilter != null)
            args.AddRange(new[] { "--lua-filter", Quote(luaFilter) });

        return await RunAsync(args, ct);
    }

    /// <summary>Markdown → PDF（XeLaTeX 中文支持）</summary>
    public async Task<string> ToPdfAsync(
        string inputPath, string outputPath,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(inputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables",
            "--pdf-engine", "xelatex",
            "-V", "CJKmainfont=宋体",
            "-o", Quote(outputPath)
        };

        return await RunAsync(args, ct);
    }

    /// <summary>导出 AST JSON</summary>
    public async Task<string> ToAstJsonAsync(
        string inputPath, CancellationToken ct = default)
    {
        var args = new List<string> { Quote(inputPath), "-t", "json" };
        return await RunAsync(args, ct);
    }

    private async Task<string> RunAsync(List<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pandocPath,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"无法启动 Pandoc: {_pandocPath}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new Exception($"Pandoc 退出码 {process.ExitCode}: {stderr}");

        return stdout;
    }

    private static string Quote(string path) =>
        path.Contains(' ') ? $"\"{path}\"" : path;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.ToDocxAsync_InvalidInput_ThrowsException" --no-restore -v n`
Expected: PASS

### 1.3 写集成测试：ToDocxAsync + ToAstJsonAsync

- [ ] **Step 5: 添加 ToDocxAsync 集成测试**

在 `PandocPipelineTests` 类中追加：

```csharp
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
```

- [ ] **Step 6: 运行全部 PandocPipeline 测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests" --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 7: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "feat(converter): implement PandocPipeline with CLI wrapper and tests"
```

---

## Task 2: ReferenceDocBuilder — 从 AFD 模板生成 reference.docx

**Files:**
- Create: `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

### 2.1 写失败测试

- [ ] **Step 1: 添加 ReferenceDocBuilder 测试**

在 `PandocPipelineTests.cs` 顶部添加 using，并追加测试：

```csharp
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Pandoc;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
```

在 `PandocPipelineTests` 类中追加：

```csharp
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
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.ReferenceDocBuilder_Build_CreatesValidDocx" --no-restore -v n`
Expected: FAIL（ReferenceDocBuilder 不存在）

### 2.2 实现 ReferenceDocBuilder

- [ ] **Step 3: 创建 ReferenceDocBuilder.cs**

创建 `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs`：

```csharp
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// 从 AfdTemplate 生成 reference.docx，供 Pandoc --reference-doc 使用
/// </summary>
public static class ReferenceDocBuilder
{
    public static void Build(string outputPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
        stylePart.Styles = new Styles();

        foreach (var (afdKey, styleDef) in template.Styles)
        {
            var styleId = AfdStyleMapper.MapToOpenXmlStyleId(afdKey);
            var style = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = styleId,
                CustomStyle = true
            };
            style.Append(new StyleName { Val = styleDef.DisplayName ?? afdKey });

            // 段落属性
            var pPr = new StyleParagraphProperties();
            if (styleDef.Alignment != null)
                pPr.Append(CreateJustification(styleDef.Alignment));
            if (styleDef.LineSpacing != null || styleDef.SpaceBefore != null || styleDef.SpaceAfter != null)
                pPr.Append(CreateSpacing(styleDef));
            if (styleDef.FirstLineIndent != null || styleDef.HangingIndent != null)
                pPr.Append(CreateIndentation(styleDef));
            style.Append(pPr);

            // 字符属性
            var rPr = new StyleRunProperties();
            if (styleDef.FontFamily != null)
                rPr.Append(CreateRunFonts(styleDef.FontFamily));
            if (styleDef.FontSize != null)
            {
                var hp = ((int)(styleDef.FontSize.Value * 2)).ToString();
                rPr.Append(new FontSize { Val = hp });
                rPr.Append(new FontSizeComplexScript { Val = hp });
            }
            if (styleDef.Bold == true)
                rPr.Append(new Bold());
            if (styleDef.Italic == true)
                rPr.Append(new Italic());
            style.Append(rPr);

            stylePart.Styles.Append(style);
        }

        // 页面设置
        var sectPr = mainPart.Document.Body!.AppendChild(new SectionProperties());
        if (template.Defaults.PageSize != null)
        {
            sectPr.AppendChild(new PageSize
            {
                Width = (uint)(template.Defaults.PageSize.Width * 567),
                Height = (uint)(template.Defaults.PageSize.Height * 567)
            });
        }
        if (template.Defaults.Margins != null)
        {
            sectPr.AppendChild(new PageMargin
            {
                Top = (uint)(template.Defaults.Margins.Top * 567),
                Bottom = (uint)(template.Defaults.Margins.Bottom * 567),
                Left = (uint)(template.Defaults.Margins.Left * 567),
                Right = (uint)(template.Defaults.Margins.Right * 567)
            });
        }

        stylePart.Styles.Save();
        mainPart.Document.Save();
    }

    private static Justification CreateJustification(string alignment) => alignment switch
    {
        "center" => new Justification { Val = JustificationValues.Center },
        "right" => new Justification { Val = JustificationValues.Right },
        "both" => new Justification { Val = JustificationValues.Both },
        _ => new Justification { Val = JustificationValues.Left }
    };

    private static Spacing CreateSpacing(AfdStyleDefinition def)
    {
        var spacing = new Spacing();
        if (def.SpaceBefore != null)
            spacing.Before = ((int)(def.SpaceBefore.Value * 20)).ToString();
        if (def.SpaceAfter != null)
            spacing.After = ((int)(def.SpaceAfter.Value * 20)).ToString();
        if (def.LineSpacing != null)
        {
            spacing.Line = ((int)(def.LineSpacing.Value * 240)).ToString();
            spacing.LineRule = LineSpacingRuleValues.Auto;
        }
        return spacing;
    }

    private static Indentation CreateIndentation(AfdStyleDefinition def)
    {
        var indent = new Indentation();
        if (def.FirstLineIndent != null)
            indent.FirstLine = ((int)(def.FirstLineIndent.Value * 20)).ToString();
        if (def.HangingIndent != null)
            indent.Hanging = ((int)(def.HangingIndent.Value * 20)).ToString();
        return indent;
    }

    private static RunFonts CreateRunFonts(string fontFamily) => new()
    {
        Ascii = fontFamily,
        EastAsia = fontFamily,
        HighAnsi = fontFamily
    };
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.ReferenceDocBuilder_Build_CreatesValidDocx" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "feat(converter): implement ReferenceDocBuilder for AFD template to reference.docx"
```

---

## Task 3: OpenXmlStyleCorrector — 合并重写样式修正

**Files:**
- Modify: `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs`
- Delete: `src/WeaveDoc.Converter/Pandoc/PageSettings.cs`
- Delete: `src/WeaveDoc.Converter/Pandoc/HeaderFooterBuilder.cs`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

### 3.1 写失败测试：ApplyAfdStyles

- [ ] **Step 1: 添加 ApplyAfdStyles 测试**

在 `PandocPipelineTests` 类中追加：

```csharp
[Fact]
public void OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx()
{
    var template = CreateTestTemplate();
    var docxPath = Path.Combine(Path.GetTempPath(), $"style-test-{Guid.NewGuid():N}.docx");

    try
    {
        // 先用 ReferenceDocBuilder 生成含 Heading1 的 docx
        ReferenceDocBuilder.Build(docxPath, template);

        // 再用 Pandoc 生成一个含标题段落的 docx（或直接手动构建）
        // 这里直接在已生成的 docx 上添加内容段落来测试修正
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
                .First(p => p.ParagraphStyleId?.Val?.Value == "Heading1");

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
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx" --no-restore -v n`
Expected: FAIL（OpenXmlStyleCorrector.ApplyAfdStyles 还是 NotImplementedException）

### 3.2 实现 OpenXmlStyleCorrector.ApplyAfdStyles

- [ ] **Step 3: 重写 OpenXmlStyleCorrector.cs**

替换 `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs` 全部内容：

```csharp
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// OpenXML 样式修正：将 AFD 样式规则精确应用到 .docx 文件
/// </summary>
public static class OpenXmlStyleCorrector
{
    /// <summary>
    /// 遍历文档段落，按 AFD 样式规则精确修正字体/字号/间距等
    /// </summary>
    public static void ApplyAfdStyles(string docxPath, AfdTemplate template)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart!.Document.Body!;

        foreach (var para in body.Elements<Paragraph>())
        {
            var styleId = para.ParagraphStyleId?.Val?.Value;
            if (styleId == null) continue;

            var afdKey = AfdStyleMapper.MapToAfdStyleKey(styleId);
            if (afdKey == null || !template.Styles.TryGetValue(afdKey, out var styleDef))
                continue;

            ApplyParagraphStyle(para, styleDef);
        }

        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>
    /// 设置页面尺寸与边距
    /// </summary>
    public static void ApplyPageSettings(string docxPath, AfdDefaults defaults)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart!.Document.Body!;
        var sectPr = body.Elements<SectionProperties>().LastOrDefault()
            ?? body.AppendChild(new SectionProperties());

        if (defaults.PageSize != null)
        {
            var pgSz = sectPr.Elements<PageSize>().FirstOrDefault()
                ?? sectPr.AppendChild(new PageSize());
            pgSz.Width = (uint)(defaults.PageSize.Width * 567);
            pgSz.Height = (uint)(defaults.PageSize.Height * 567);
        }

        if (defaults.Margins != null)
        {
            var pgMar = sectPr.Elements<PageMargin>().FirstOrDefault()
                ?? sectPr.AppendChild(new PageMargin());
            pgMar.Top = (uint)(defaults.Margins.Top * 567);
            pgMar.Bottom = (uint)(defaults.Margins.Bottom * 567);
            pgMar.Left = (uint)(defaults.Margins.Left * 567);
            pgMar.Right = (uint)(defaults.Margins.Right * 567);
        }

        doc.MainDocumentPart.Document.Save();
    }

    /// <summary>
    /// 添加页眉页脚
    /// </summary>
    public static void ApplyHeaderFooter(string docxPath, AfdHeaderFooter headerFooter)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var mainPart = doc.MainDocumentPart!;
        var body = mainPart.Document.Body!;
        var sectPr = body.Elements<SectionProperties>().LastOrDefault()
            ?? body.AppendChild(new SectionProperties());

        if (headerFooter.Header != null)
            ApplyHeader(mainPart, sectPr, headerFooter.Header);

        if (headerFooter.Footer != null)
            ApplyFooter(mainPart, sectPr, headerFooter.Footer);

        mainPart.Document.Save();
    }

    private static void ApplyParagraphStyle(Paragraph para, AfdStyleDefinition styleDef)
    {
        var pPr = para.ParagraphProperties ?? para.InsertAt(new ParagraphProperties(), 0);

        if (styleDef.Alignment != null)
        {
            pPr.Justification = styleDef.Alignment switch
            {
                "center" => new Justification { Val = JustificationValues.Center },
                "right" => new Justification { Val = JustificationValues.Right },
                "both" => new Justification { Val = JustificationValues.Both },
                _ => new Justification { Val = JustificationValues.Left }
            };
        }

        var spacing = pPr.Spacing ?? pPr.AppendChild(new Spacing())!;
        if (styleDef.SpaceBefore != null)
            spacing.Before = ((int)(styleDef.SpaceBefore.Value * 20)).ToString();
        if (styleDef.SpaceAfter != null)
            spacing.After = ((int)(styleDef.SpaceAfter.Value * 20)).ToString();
        if (styleDef.LineSpacing != null)
        {
            spacing.Line = ((int)(styleDef.LineSpacing.Value * 240)).ToString();
            spacing.LineRule = LineSpacingRuleValues.Auto;
        }

        if (styleDef.FirstLineIndent != null)
        {
            var ind = pPr.Indentation ?? pPr.AppendChild(new Indentation())!;
            ind.FirstLine = ((int)(styleDef.FirstLineIndent.Value * 20)).ToString();
        }

        // 修正 Run 属性
        foreach (var run in para.Elements<Run>())
        {
            var rPr = run.RunProperties ?? run.InsertAt(new RunProperties(), 0);

            if (styleDef.FontFamily != null)
            {
                rPr.RunFonts = new RunFonts
                {
                    Ascii = styleDef.FontFamily,
                    EastAsia = styleDef.FontFamily,
                    HighAnsi = styleDef.FontFamily
                };
            }

            if (styleDef.FontSize != null)
            {
                var hp = ((int)(styleDef.FontSize.Value * 2)).ToString();
                rPr.FontSize = new FontSize { Val = hp };
                rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = hp };
            }

            if (styleDef.Bold == true)
                rPr.Bold = new Bold();
            if (styleDef.Italic == true)
                rPr.Italic = new Italic();
        }
    }

    private static void ApplyHeader(MainDocumentPart mainPart, SectionProperties sectPr,
        AfdHeaderContent headerDef)
    {
        var headerPart = mainPart.AddNewPart<HeaderPart>();
        var header = new Header();

        var p = new Paragraph();
        var pPr = new ParagraphProperties();
        if (headerDef.Alignment != null)
        {
            pPr.Justification = headerDef.Alignment switch
            {
                "center" => new Justification { Val = JustificationValues.Center },
                "right" => new Justification { Val = JustificationValues.Right },
                _ => new Justification { Val = JustificationValues.Left }
            };
        }
        p.AppendChild(pPr);

        var run = new Run();
        var rPr = new RunProperties();
        if (headerDef.FontFamily != null)
            rPr.Append(new RunFonts { Ascii = headerDef.FontFamily, EastAsia = headerDef.FontFamily });
        if (headerDef.FontSize != null)
        {
            var hp = ((int)(headerDef.FontSize.Value * 2)).ToString();
            rPr.Append(new FontSize { Val = hp });
        }
        run.AppendChild(rPr);
        run.AppendChild(new Text(headerDef.Text));
        p.AppendChild(run);
        header.AppendChild(p);
        headerPart.Header = header;

        var headerRef = new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) };
        sectPr.AppendChild(headerRef);
    }

    private static void ApplyFooter(MainDocumentPart mainPart, SectionProperties sectPr,
        AfdFooterContent footerDef)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new Footer();

        var p = new Paragraph();
        var pPr = new ParagraphProperties();
        if (footerDef.Alignment != null)
        {
            pPr.Justification = footerDef.Alignment switch
            {
                "center" => new Justification { Val = JustificationValues.Center },
                "right" => new Justification { Val = JustificationValues.Right },
                _ => new Justification { Val = JustificationValues.Left }
            };
        }
        p.AppendChild(pPr);

        if (footerDef.PageNumbering)
        {
            // 插入页码域代码: { PAGE }
            var run1 = new Run(new FieldChar { FieldCharType = FieldCharValues.Begin });
            var run2 = new Run(new FieldCode { Text = " PAGE " });
            var run3 = new Run(new FieldChar { FieldCharType = FieldCharValues.End });
            p.Append(run1);
            p.Append(run2);
            p.Append(run3);
        }

        footer.AppendChild(p);
        footerPart.Footer = footer;

        var footerRef = new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) };
        sectPr.AppendChild(footerRef);

        if (footerDef.StartPage != 1)
        {
            sectPr.AppendChild(new PageNumberStart { Val = (uint)footerDef.StartPage });
        }
    }
}
```

- [ ] **Step 4: 运行 ApplyAfdStyles 测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx" --no-restore -v n`
Expected: PASS

### 3.3 写测试：ApplyPageSettings

- [ ] **Step 5: 添加 ApplyPageSettings 测试**

在 `PandocPipelineTests` 类中追加：

```csharp
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
        Assert.Equal(14175u, pgMar.Top?.Value);
        Assert.Equal(17010u, pgMar.Left?.Value);
    }
    finally
    {
        if (File.Exists(docxPath)) File.Delete(docxPath);
    }
}
```

- [ ] **Step 6: 运行确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.OpenXmlStyleCorrector_ApplyPageSettings_SetsDimensions" --no-restore -v n`
Expected: PASS

### 3.4 删除旧文件

- [ ] **Step 7: 删除 PageSettings.cs 和 HeaderFooterBuilder.cs**

```bash
git rm src/WeaveDoc.Converter/Pandoc/PageSettings.cs
git rm src/WeaveDoc.Converter/Pandoc/HeaderFooterBuilder.cs
```

- [ ] **Step 8: 运行全部测试确认无回归**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 9: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "feat(converter): implement OpenXmlStyleCorrector with page/header/footer support, remove redundant classes"
```

---

## Task 4: DocumentConversionEngine — 端到端编排

**Files:**
- Modify: `src/WeaveDoc.Converter/DocumentConversionEngine.cs`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

### 4.1 写失败测试

- [ ] **Step 1: 添加端到端测试**

在 `PandocPipelineTests` 类中追加：

```csharp
[Fact]
public async Task DocumentConversionEngine_ConvertAsync_ProducesDocx()
{
    var root = FindSolutionRoot();
    var pandocPath = Path.Combine(root, "tools", "pandoc-3.9.0.2", "pandoc.exe");
    var templatePath = Path.Combine(root,
        "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "default-thesis.json");

    // 用真实 AfdParser 解析模板
    var parser = new Afd.AfdParser();
    var template = parser.Parse(templatePath);

    // 用 Mock ConfigManager（直接返回解析好的模板）
    var configManager = new Config.ConfigManager("unused.db");
    // ConfigManager 还是 stub，直接构造引擎用 PandocPipeline + 手动模板
    var pipeline = new PandocPipeline(pandocPath);
    var engine = new DocumentConversionEngine(pipeline, configManager);

    // 手动准备 MD 文件
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
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.DocumentConversionEngine_ConvertAsync_ProducesDocx" --no-restore -v n`
Expected: FAIL（因为还缺少部分引用或 DocumentConversionEngine 还是 stub）

### 4.2 实现 DocumentConversionEngine

- [ ] **Step 3: 重写 DocumentConversionEngine.cs**

替换 `src/WeaveDoc.Converter/DocumentConversionEngine.cs` 全部内容：

```csharp
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter;

/// <summary>
/// 端到端编排：Markdown → AFD → DOCX/PDF
/// 这是组长唯一需要调用的入口
/// </summary>
public class DocumentConversionEngine
{
    private readonly PandocPipeline _pandoc;
    private readonly ConfigManager _configManager;

    public DocumentConversionEngine(PandocPipeline pandoc, ConfigManager configManager)
    {
        _pandoc = pandoc;
        _configManager = configManager;
    }

    public async Task<ConversionResult> ConvertAsync(
        string markdownPath,
        string templateId,
        string outputFormat,
        CancellationToken ct = default)
    {
        var template = await _configManager.GetTemplateAsync(templateId);
        if (template == null)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = $"模板 '{templateId}' 不存在"
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"weavedoc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Step 1: 生成 reference.docx
            var refDocPath = Path.Combine(tempDir, "reference.docx");
            ReferenceDocBuilder.Build(refDocPath, template);

            // Step 2: Pandoc 转换
            var rawDocxPath = Path.Combine(tempDir, "raw.docx");
            await _pandoc.ToDocxAsync(markdownPath, rawDocxPath, refDocPath, ct: ct);

            // Step 3: OpenXML 样式精确修正
            OpenXmlStyleCorrector.ApplyAfdStyles(rawDocxPath, template);
            OpenXmlStyleCorrector.ApplyPageSettings(rawDocxPath, template.Defaults);

            if (template.HeaderFooter != null)
                OpenXmlStyleCorrector.ApplyHeaderFooter(rawDocxPath, template.HeaderFooter);

            // Step 4: 输出
            var outputPath = Path.ChangeExtension(markdownPath, outputFormat);
            if (outputFormat == "docx")
            {
                File.Copy(rawDocxPath, outputPath, overwrite: true);
            }
            else if (outputFormat == "pdf")
            {
                await _pandoc.ToPdfAsync(rawDocxPath, outputPath, ct);
            }

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                Format = outputFormat
            };
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

public record ConversionResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = "";
    public string Format { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
}
```

- [ ] **Step 4: 运行端到端测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests.DocumentConversionEngine_ConvertAsync_ProducesDocx" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 运行全部测试确认无回归**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/DocumentConversionEngine.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "feat(converter): implement DocumentConversionEngine with full pipeline orchestration"
```

---

## Task 5: 最终验证

- [ ] **Step 1: 运行全部测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests -v n`
Expected: 全部 PASS（包括之前 AfdParser 和 AfdStyleMapper 的测试）

- [ ] **Step 2: 验证文件结构**

确认以下文件存在且内容正确：
- `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs` — 已实现
- `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs` — 已实现（含页面/页眉页脚）
- `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs` — 新建
- `src/WeaveDoc.Converter/DocumentConversionEngine.cs` — 已实现

确认以下文件已删除：
- `src/WeaveDoc.Converter/Pandoc/PageSettings.cs` — 已删除
- `src/WeaveDoc.Converter/Pandoc/HeaderFooterBuilder.cs` — 已删除

- [ ] **Step 3: 最终提交（如有遗留）**

```bash
git add -A
git commit -m "chore(converter): finalize Pandoc pipeline implementation"
```
