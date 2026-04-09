# 测试深度加强设计

日期：2026-04-09
状态：待实施

## 背景

PandocPipelineTests 中有 5 个测试仅验证文件产出（存在 + 大小 > 0 或能被 OpenXML 打开），未验证文档内容是否正确。这些浅层冒烟测试无法捕获样式未应用、内容丢失等回归问题。

## 加强范围

| # | 测试方法 | 当前验证 | 加强后验证 |
|---|---------|---------|-----------|
| 1 | `ToDocxAsync_WithInput_ProducesDocx` | 文件存在 + 大小 > 0 | 打开 DOCX 验证包含"测试标题"和"正文段落"文本段落 |
| 2 | `FullPipeline_..._ProducesValidDocx` | 能被 OpenXML 打开 | Heading1 段落字体=黑体、字号=44半磅；页面尺寸 twip 值 |
| 3 | `DCE_ConvertAsync_Docx` | 成功 + 可打开 | Heading1 Run 字体=黑体、字号=32半磅 |
| 4 | `FromDocxToPdfAsync_..._ProducesPdf` | 文件存在 + 大小 > 0 | PDF 魔数 `%PDF-` + 文件大小 > 1KB |
| 5 | `DCE_ConvertAsync_Pdf` | 成功 + 大小 > 0 | PDF 魔数 `%PDF-` + 文件大小 > 1KB |

## 详细设计

### 测试 1：ToDocxAsync_WithInput_ProducesDocx

裸 Pandoc 调用（无 reference.docx），输入 `# 测试标题\n\n这是正文段落。\n`。

补充断言：
- 用 `WordprocessingDocument.Open` 打开输出文件
- `body.Descendants<Paragraph>()` 中存在 `InnerText` 包含"测试标题"的段落
- 存在 `InnerText` 包含"正文段落"的段落

不验证字体/字号——无 reference.docx 时 Pandoc 使用默认字体，不可控。

### 测试 2：FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx

完整管线 + `default-thesis.json` 真实模板。模板 heading1：字体"黑体"、字号 22pt。

补充断言：
- `body.Descendants<Paragraph>()` 找到 `InnerText == "测试论文标题"` 的段落
- 该段落第一个 `Run` 的 `RunProperties.RunFonts.EastAsia` = "黑体"
- 该 `Run` 的 `RunProperties.FontSize.Val` = "44"（22pt = 44 半磅）
- `body.Elements<SectionProperties>()` 的 `PageSize.Width/Height` 与模板 `AfdDefaults.PageSize` 计算值一致（mm × 567）

### 测试 3：DocumentConversionEngine_ConvertAsync_Docx

DCE 端到端 + `CreateTestTemplate()`，heading1：字体"黑体"、字号 16pt。

补充断言：
- `body.Descendants<Paragraph>()` 找到包含"测试标题"且 `ParagraphStyleId.Val == "Heading1"` 的段落
- 该段落第一个 `Run` 的 `RunProperties.RunFonts.EastAsia` = "黑体"
- 该 `Run` 的 `RunProperties.FontSize.Val` = "32"（16pt = 32 半磅）

### 测试 4 & 5：PDF 魔数验证

新增私有辅助方法：

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

两个 PDF 测试中用 `AssertIsPdf(pdfPath)` 替换原有的 `Assert.True(File.Exists)` + `Assert.True(Length > 0)` 断言。

### 变更范围

仅修改 `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs`：
- 在 5 个测试方法中补充内容断言
- 新增 1 个私有辅助方法 `AssertIsPdf`
- 不新增测试方法、不引入 NuGet 依赖

## 不在范围内

- 重构测试结构或拆分测试类
- 引入 PDF 解析库验证页数/文本内容
- 修改 AfdParser/AfdStyleMapper/ConfigManager/BibtexParser 测试
