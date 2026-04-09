# 生产就绪性瑕疵修复 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 3 个生产就绪性瑕疵：表格段落遗漏、Normal 样式缺失、PDF 管道绕过 AFD 修正 + 字体硬编码。

**Architecture:** 方案 B——新增 `FromDocxToPdfAsync` 方法从已修正 DOCX 生成 PDF（Pandoc DOCX→PDF），字体参数化传入；表格段落修复一行；Normal 样式十行补丁。TDD 驱动。

**Tech Stack:** C# / .NET 10, OpenXML SDK 3.5.1, Pandoc CLI, Tectonic, xUnit

**Spec:** `docs/superpowers/specs/2026-04-09-production-bugfix-design.md`

---

## File Structure

| 操作 | 文件 | 职责 |
|------|------|------|
| Modify | `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs:23` | Elements→Descendants，覆盖表格内段落 |
| Modify | `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs:23-26` | 在样式循环前创建 Normal 样式 |
| Modify | `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs:63` | 新增 FromDocxToPdfAsync 方法 |
| Modify | `src/WeaveDoc.Converter/DocumentConversionEngine.cs:64-66` | PDF 分支改用修正后 DOCX |
| Modify | `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` | 新增测试用例 |

---

### Task 1: OpenXmlStyleCorrector — 表格内段落遗漏修复

**Files:**
- Modify: `src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs:23`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 写失败测试 — 验证表格内段落被修正**

在 `PandocPipelineTests.cs` 末尾（类的最后一个 `}` 之前）添加：

```csharp
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
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "StylesTableCellParagraphs" --no-restore -v n`
Expected: FAIL — 表格内段落的字体未被修正（`Elements` 跳过了表格内的段落）

- [ ] **Step 3: 实现 — 一行修复**

在 `OpenXmlStyleCorrector.cs` 第 23 行：

```csharp
// 之前
foreach (var paragraph in body.Elements<Paragraph>())
// 之后
foreach (var paragraph in body.Descendants<Paragraph>())
```

- [ ] **Step 4: 运行测试，确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "StylesTableCellParagraphs" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "fix: use Descendants instead of Elements to style table cell paragraphs"
```

---

### Task 2: ReferenceDocBuilder — Normal 样式缺失修复

**Files:**
- Modify: `src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs:23-26`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 写失败测试 — 验证 Normal 样式存在**

在 `PandocPipelineTests.cs` 的 `ReferenceDocBuilder_Build_CreatesValidDocx` 测试方法中，在 `Assert.Equal("32", fontSize.Val?.Value);` 之后、`try` 块结束的 `}` 之前，添加：

```csharp
// 验证 Normal 样式存在且使用模板默认字体
var normal = stylesPart.Styles!.Elements<Style>()
    .FirstOrDefault(s => s.StyleId == "Normal");
Assert.NotNull(normal);
var normalRPr = normal.Elements<StyleRunProperties>().First();
var normalFonts = normalRPr.Elements<RunFonts>().First();
Assert.Equal("宋体", normalFonts.EastAsia?.Value);
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "ReferenceDocBuilder_Build_CreatesValidDocx" --no-restore -v n`
Expected: FAIL — `normal` 为 null

- [ ] **Step 3: 实现 — 在 ReferenceDocBuilder.Build 中创建 Normal 样式**

在 `ReferenceDocBuilder.cs` 的 `Build` 方法中，将：

```csharp
foreach (var (afdKey, styleDef) in template.Styles)
```

替换为：

```csharp
// 先创建 Normal（默认段落）样式，确保 Pandoc 生成的正文段落有样式基准
var normalStyle = new Style
{
    Type = StyleValues.Paragraph,
    StyleId = "Normal"
};
normalStyle.Append(new StyleName { Val = "Normal" });
var normalRPr = new StyleRunProperties();
normalRPr.Append(CreateRunFonts(template.Defaults.FontFamily));
if (template.Defaults.FontSize != null)
{
    var hp = ((int)(template.Defaults.FontSize.Value * 2)).ToString();
    normalRPr.Append(new FontSize { Val = hp });
    normalRPr.Append(new FontSizeComplexScript { Val = hp });
}
normalStyle.Append(normalRPr);
stylePart.Styles.Append(normalStyle);

// 再创建模板中定义的各样式
foreach (var (afdKey, styleDef) in template.Styles)
```

- [ ] **Step 4: 运行测试，确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "ReferenceDocBuilder_Build_CreatesValidDocx" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "fix: add Normal style to reference.docx for Pandoc body paragraph matching"
```

---

### Task 3: PandocPipeline — 新增 FromDocxToPdfAsync（字体参数化）

**Files:**

- Modify: `src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs`（新增方法）
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 写失败测试**

在 `PandocPipelineTests.cs` 末尾添加：

```csharp
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

        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 0);
    }
    finally
    {
        File.Delete(mdPath);
        if (File.Exists(docxPath)) File.Delete(docxPath);
        if (File.Exists(pdfPath)) File.Delete(pdfPath);
    }
}
```

- [ ] **Step 2: 运行测试，确认编译失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "FromDocxToPdfAsync_WithDocxInput_ProducesPdf" --no-restore -v n`
Expected: 编译失败 — `FromDocxToPdfAsync` 方法不存在

- [ ] **Step 3: 实现 — 新增方法**

在 `PandocPipeline.cs` 的 `ToPdfAsync` 方法之后（第 63 行 `}` 之后）、`ToAstJsonAsync` 方法之前插入：

```csharp
/// <summary>DOCX → PDF（基于已修正的 DOCX，保留 AFD 样式）</summary>
public async Task<string> FromDocxToPdfAsync(
    string docxInputPath, string outputPath,
    string? mainFont = null, string? cjkMainFont = null, string? monoFont = null,
    CancellationToken ct = default)
{
    var args = new List<string>
    {
        Quote(docxInputPath),
        "-f", "docx",
        "--pdf-engine", "tectonic",
        "-o", Quote(outputPath)
    };

    if (mainFont != null)
        args.AddRange(new[] { "-V", $"mainfont={mainFont}" });
    if (cjkMainFont != null)
        args.AddRange(new[] { "-V", $"CJKmainfont={cjkMainFont}" });
    if (monoFont != null)
        args.AddRange(new[] { "-V", $"monofont={monoFont}" });

    return await RunAsync(args, ct);
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "FromDocxToPdfAsync_WithDocxInput_ProducesPdf" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "feat: add FromDocxToPdfAsync with parameterized fonts for DOCX-to-PDF conversion"
```

---

### Task 4: DocumentConversionEngine — PDF 分支改用修正后 DOCX

**依赖 Task 3**（需要 `FromDocxToPdfAsync` 方法已存在）

**Files:**

- Modify: `src/WeaveDoc.Converter/DocumentConversionEngine.cs:64-66`
- Test: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 写测试 — DCE PDF 端到端转换**

在 `PandocPipelineTests.cs` 末尾添加：

```csharp
[Fact]
public async Task DocumentConversionEngine_ConvertAsync_Pdf()
{
    var root = FindSolutionRoot();
    var pandocPath = Path.Combine(root, "tools", "pandoc", "pandoc.exe");
    var dbPath = Path.Combine(Path.GetTempPath(), $"dce-pdf-{Guid.NewGuid():N}.db");

    try
    {
        var configManager = new ConfigManager(dbPath);
        var template = CreateTestTemplate();
        await configManager.SaveTemplateAsync("test-tpl", template);

        var pipeline = new PandocPipeline(pandocPath);
        var engine = new DocumentConversionEngine(pipeline, configManager);

        var mdPath = Path.Combine(Path.GetTempPath(), $"dce-pdf-{Guid.NewGuid():N}.md");
        File.WriteAllText(mdPath, "# PDF测试标题\n\n这是PDF正文内容。\n");

        try
        {
            var result = await engine.ConvertAsync(mdPath, "test-tpl", "pdf");

            Assert.True(result.Success, $"PDF 转换失败: {result.ErrorMessage}");
            Assert.True(File.Exists(result.OutputPath), "PDF 输出文件不存在");
            Assert.EndsWith(".pdf", result.OutputPath);
            Assert.True(new FileInfo(result.OutputPath).Length > 0, "PDF 文件为空");
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
```

- [ ] **Step 2: 运行测试，观察当前行为**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "DocumentConversionEngine_ConvertAsync_Pdf" --no-restore -v n`
Expected: PASS（使用旧路径 `ToPdfAsync`，PDF 不含 AFD 样式）。此测试锁定行为基线。

- [ ] **Step 3: 实现 — DCE PDF 分支改用修正后 DOCX**

在 `DocumentConversionEngine.cs` 中，将：

```csharp
else if (outputFormat == "pdf")
{
    await _pandoc.ToPdfAsync(markdownPath, outputPath, ct);
}
```

替换为：

```csharp
else if (outputFormat == "pdf")
{
    var font = template.Defaults.FontFamily;
    await _pandoc.FromDocxToPdfAsync(rawDocxPath, outputPath, font, font, font, ct);
}
```

- [ ] **Step 4: 运行测试，确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "DocumentConversionEngine_ConvertAsync_Pdf" --no-restore -v n`
Expected: PASS — PDF 从已修正 DOCX 生成，包含 AFD 样式

- [ ] **Step 5: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/DocumentConversionEngine.cs tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "fix: generate PDF from corrected DOCX to include AFD styles and page settings"
```

---

## Self-Review

**1. Spec coverage:**
- Fix 1a (字体硬编码): Task 3 (FromDocxToPdfAsync 参数化) ✓
- Fix 1b (PDF 绕过修正): Task 4 (DCE 改用修正后 DOCX) ✓
- Fix 2 (表格段落遗漏): Task 1 (Descendants) ✓
- Fix 3 (Normal 样式缺失): Task 2 (ReferenceDocBuilder) ✓

**2. Placeholder scan:** 无 TBD/TODO/待定。

**3. Type consistency:**
- `AfdDefaults.FontFamily` → `string` (默认 "宋体") — Task 2 (Normal) 和 Task 4 (PDF)，类型一致
- `AfdDefaults.FontSize` → `double?` — Task 2，与 `AfdStyleDefinition.FontSize` 类型一致
- `FromDocxToPdfAsync` 在 Task 3 定义，Task 4 调用 — `font, font, font` 对应 `mainFont, cjkMainFont, monoFont`，匹配
- 测试中 `CreateTestTemplate()` 的 `FontFamily = "宋体"`, `FontSize = 12` — 与所有验证断言一致

**4. 依赖关系:** Task 4 依赖 Task 3（`FromDocxToPdfAsync` 必须先存在）。其余 Task 无依赖，可并行执行。
