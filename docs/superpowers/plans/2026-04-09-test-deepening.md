# 测试深度加强 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 加强 PandocPipelineTests 中 5 个浅层冒烟测试，使其验证实际文档内容而非仅检查文件存在。

**Architecture:** 原地加强方案——在现有测试方法中补充 OpenXML 内容断言和 PDF 魔数校验，不新增测试方法。新增一个 `AssertIsPdf` 私有辅助方法消除 PDF 验证重复。

**Tech Stack:** C# / .NET 10, xUnit, DocumentFormat.OpenXml 3.5.1

**Spec:** `docs/superpowers/specs/2026-04-09-test-deepening-design.md`

> **注意：** Spec 中测试 2 的字号标注为 22pt/44半磅 有误，default-thesis.json 实际 heading1 fontSize 为 16pt/32半磅。以下计划使用正确值。

---

## File Structure

| 操作 | 文件 | 职责 |
|------|------|------|
| Modify | `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` | 加强 5 个测试 + 新增 AssertIsPdf 辅助方法 |

---

### Task 1: PDF 测试加强（AssertIsPdf + 两个 PDF 测试）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 添加 AssertIsPdf 辅助方法**

在 `PandocPipelineTests` 类的最后一个 `}` 之前（`DocumentConversionEngine_ConvertAsync_Pdf` 测试之后），添加：

```csharp
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
```

- [ ] **Step 2: 加强 FromDocxToPdfAsync_WithDocxInput_ProducesPdf**

在该测试中，将：

```csharp
Assert.True(File.Exists(pdfPath));
Assert.True(new FileInfo(pdfPath).Length > 0);
```

替换为：

```csharp
AssertIsPdf(pdfPath);
```

- [ ] **Step 3: 加强 DocumentConversionEngine_ConvertAsync_Pdf**

在该测试中，将：

```csharp
Assert.True(File.Exists(result.OutputPath), "PDF 输出文件不存在");
Assert.EndsWith(".pdf", result.OutputPath);
Assert.True(new FileInfo(result.OutputPath).Length > 0, "PDF 文件为空");
```

替换为：

```csharp
Assert.True(File.Exists(result.OutputPath), "PDF 输出文件不存在");
Assert.EndsWith(".pdf", result.OutputPath);
AssertIsPdf(result.OutputPath);
```

- [ ] **Step 4: 运行 PDF 相关测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "FromDocxToPdfAsync_WithDocxInput_ProducesPdf|DocumentConversionEngine_ConvertAsync_Pdf" --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 5: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 6: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: strengthen PDF tests with magic number and minimum size validation"
```

---

### Task 2: DOCX 测试加强（ToDocxAsync + FullPipeline + DCE_Docx）

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`

- [ ] **Step 1: 加强 ToDocxAsync_WithInput_ProducesDocx**

在该测试中，将：

```csharp
Assert.True(File.Exists(docxPath));
Assert.True(new FileInfo(docxPath).Length > 0);
```

替换为：

```csharp
Assert.True(File.Exists(docxPath));

using var doc = WordprocessingDocument.Open(docxPath, false);
var body = doc.MainDocumentPart!.Document.Body!;
var paragraphs = body.Descendants<Paragraph>().ToList();
Assert.Contains(paragraphs, p => p.InnerText.Contains("测试标题"));
Assert.Contains(paragraphs, p => p.InnerText.Contains("正文段落"));
```

- [ ] **Step 2: 运行测试验证**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "ToDocxAsync_WithInput_ProducesDocx" --no-restore -v n`
Expected: PASS

- [ ] **Step 3: 加强 FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx**

在该测试中，将：

```csharp
Assert.True(File.Exists(rawDocxPath));

// 验证输出可以被 OpenXML 正确打开
using var doc = WordprocessingDocument.Open(rawDocxPath, false);
Assert.NotNull(doc.MainDocumentPart);
Assert.NotNull(doc.MainDocumentPart.Document.Body);
```

替换为：

```csharp
Assert.True(File.Exists(rawDocxPath));

using var doc = WordprocessingDocument.Open(rawDocxPath, false);
var body = doc.MainDocumentPart!.Document.Body!;

// 验证 Heading1 段落样式（default-thesis.json: 黑体、16pt）
var heading = body.Descendants<Paragraph>()
    .First(p => p.InnerText == "测试论文标题");
var run = heading.Elements<Run>().First();
var rPr = run.RunProperties;
Assert.NotNull(rPr);
Assert.Equal("黑体", rPr.Elements<RunFonts>().First().EastAsia?.Value);
Assert.Equal("32", rPr.Elements<FontSize>().First().Val?.Value); // 16pt = 32 half-points

// 验证页面尺寸（210mm × 297mm × 567 = twips）
var sectPr = body.Elements<SectionProperties>().First();
var pgSz = sectPr.Elements<PageSize>().First();
Assert.Equal(119070u, pgSz.Width?.Value);
Assert.Equal(168399u, pgSz.Height?.Value);
```

- [ ] **Step 4: 运行测试验证**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx" --no-restore -v n`
Expected: PASS

- [ ] **Step 5: 加强 DocumentConversionEngine_ConvertAsync_Docx**

在该测试中，将：

```csharp
using var doc = WordprocessingDocument.Open(result.OutputPath, false);
Assert.NotNull(doc.MainDocumentPart);
Assert.NotNull(doc.MainDocumentPart.Document.Body);
```

替换为：

```csharp
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
```

- [ ] **Step 6: 运行测试验证**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --filter "DocumentConversionEngine_ConvertAsync_Docx" --no-restore -v n`
Expected: PASS

- [ ] **Step 7: 运行全量测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n`
Expected: 全部 PASS

- [ ] **Step 8: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs
git commit -m "test: strengthen DOCX tests with OpenXML content assertions for styles and page dimensions"
```

---

## Self-Review

**1. Spec coverage:**
- 测试 1（ToDocxAsync 内容验证）→ Task 2 Step 1 ✓
- 测试 2（FullPipeline 字体/字号/页面尺寸）→ Task 2 Step 3 ✓
- 测试 3（DCE_Docx 字体/字号）→ Task 2 Step 5 ✓
- 测试 4（FromDocxToPdfAsync 魔数）→ Task 1 Step 2 ✓
- 测试 5（DCE_Pdf 魔数）→ Task 1 Step 3 ✓
- AssertIsPdf 辅助方法 → Task 1 Step 1 ✓

**2. Placeholder scan:** 无 TBD/TODO/待定。

**3. Type consistency:**
- `AssertIsPdf` 参数 `string path` — 两个调用点传入 `pdfPath` (string) 和 `result.OutputPath` (string)，匹配
- `RunFonts.EastAsia?.Value` 返回 `string?`，`Assert.Equal` 期望 string — 匹配
- `FontSize.Val?.Value` 返回 `string?`，`Assert.Equal` 期望 string — 匹配
- `PageSize.Width?.Value` 返回 `UInt32Value?`，`Assert.Equal(119070u, ...)` 用 uint — 匹配
- heading1 字号 16pt = "32" 半磅 — 与 default-thesis.json `fontSize: 16` 一致 ✓

**4. Spec 修正记录：** Spec 中测试 2 标注 22pt/44半磅，经验证 default-thesis.json 实际为 16pt/32半磅。计划使用正确值。
