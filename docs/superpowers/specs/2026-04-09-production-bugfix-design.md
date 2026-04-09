# 生产就绪性瑕疵修复设计

日期：2026-04-09
状态：待实施

## 背景

生产就绪性评估发现 4 个瑕疵，其中 3 个可直接修复，第 4 个（BibtexParser 未集成）需等待上游模块。

## 修复范围

| # | 瑕疵 | 文件 | 严重性 |
|---|------|------|--------|
| 1 | PDF 字体硬编码 SimSun + PDF 绕过 OpenXML 修正 | PandocPipeline.cs, DocumentConversionEngine.cs | 高 |
| 2 | 未处理表格内段落 | OpenXmlStyleCorrector.cs | 中 |
| 3 | reference.docx 缺少 Normal 样式 | ReferenceDocBuilder.cs | 中 |

## 方案选择

**选定方案 B：基于 DOCX 生成 PDF**

- 新增 `FromDocxToPdfAsync` 方法，从已修正 DOCX 生成 PDF
- PDF 输出完整包含 AFD 样式、页面设置、页眉页脚
- 改动约 30 行，无过度抽象

排除方案：
- A（最小补丁）：PDF 仍缺页面设置/页眉页脚
- C（全面重构）：过度工程

## 详细设计

### Fix 1：PDF 管道修复

#### 1a. PandocPipeline — 新增 FromDocxToPdfAsync

文件：`src/WeaveDoc.Converter/Pandoc/PandocPipeline.cs`

新增方法签名：

```csharp
public async Task<string> FromDocxToPdfAsync(
    string docxInputPath, string outputPath,
    string? mainFont = null, string? cjkMainFont = null, string? monoFont = null,
    CancellationToken ct = default)
```

行为：
- `-f docx` 读取已修正的 DOCX 文件
- 字体通过可选参数传入，不硬编码
- 保留现有 `ToPdfAsync` 不变（向后兼容）

#### 1b. DocumentConversionEngine — PDF 分支

文件：`src/WeaveDoc.Converter/DocumentConversionEngine.cs`

变更点：第 64-66 行

```csharp
// 之前
await _pandoc.ToPdfAsync(markdownPath, outputPath, ct);

// 之后
var font = template.Defaults.FontFamily; // 默认 "宋体"
await _pandoc.FromDocxToPdfAsync(rawDocxPath, outputPath, font, font, font, ct);
```

数据流变化：

```
之前: Markdown ──Pandoc──→ PDF (无 AFD 样式)
之后: Markdown → DOCX → AFD修正 → 修正后DOCX ──Pandoc──→ PDF (完整 AFD 样式)
```

### Fix 2：表格内段落遗漏

文件：`src/WeaveDoc.Converter/Pandoc/OpenXmlStyleCorrector.cs`
位置：第 23 行

```csharp
// 之前
foreach (var paragraph in body.Elements<Paragraph>())
// 之后
foreach (var paragraph in body.Descendants<Paragraph>())
```

`Elements<T>()` 仅遍历直接子级，跳过表格单元格内的段落。
`Descendants<T>()` 遍历整个文档树，覆盖所有容器内的段落。

### Fix 3：Normal 样式缺失

文件：`src/WeaveDoc.Converter/Pandoc/ReferenceDocBuilder.cs`
位置：`Build` 方法，`foreach (var (afdKey, styleDef) in template.Styles)` 循环之前

在模板样式循环之前，显式创建 Normal 样式：

```csharp
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
```

数据源：`template.Defaults.FontFamily`（默认值 `"宋体"`）和 `template.Defaults.FontSize`。

## 不在范围内

- BibtexParser 集成（等待上游模块定义需求）
- DCE 异常信息泄露 `ex.ToString()`（属于安全加固，非本次范围）
- Pandoc/Tectonic 存在性预检（属于健壮性增强，非本次范围）

## 影响分析

| 修改 | 影响范围 | 向后兼容 |
|------|----------|----------|
| PandocPipeline 新增方法 | 仅新增，不修改现有 API | 完全兼容 |
| DCE PDF 分支 | 仅影响 `outputFormat == "pdf"` 路径 | DOCX 路径不变 |
| Descendants 替换 Elements | 影响所有段落遍历 | 行为扩展，不破坏现有 |
| Normal 样式创建 | 仅影响 reference.docx 生成 | 补充缺失，不冲突 |
