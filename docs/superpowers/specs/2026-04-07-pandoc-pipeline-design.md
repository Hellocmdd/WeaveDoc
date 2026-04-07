# Pandoc 转换管道设计文档

> **模块**: 3.2 Pandoc 转换管道
> **日期**: 2026-04-07
> **状态**: 待批准
> **涉及文件**: `PandocPipeline.cs`, `OpenXmlStyleCorrector.cs`, `ReferenceDocBuilder.cs`, `DocumentConversionEngine.cs`, `PandocPipelineTests.cs`

---

## 1. 概述

实现 WeaveDoc 语义转换系统的核心管道：Markdown → AFD 样式注入 → DOCX/PDF 导出。采用两阶段管道架构（reference-doc 预生成 + OpenXML 后处理修正），确保 Pandoc 转换结果精确匹配 AFD 模板定义的学术排版规范。

## 2. 架构决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 管道策略 | 两阶段（reference-doc + 后处理） | Pandoc 负责结构映射，OpenXML 负责精确修正，各司其职 |
| DocumentConversionEngine 接口 | 保持 templateId (string) | 运行时通过 ConfigManager 查找模板，符合现有 stub |
| 类结构 | 合并 PageSettings/HeaderFooterBuilder → OpenXmlStyleCorrector | 消除功能重叠，统一所有 OpenXML 修正操作 |
| Pandoc 路径 | tools/pandoc-3.9.0.2/pandoc.exe | 本地安装，不污染系统环境 |

## 3. 数据流

```
ConvertAsync(markdownPath, templateId, outputFormat)
  │
  ├─ 1. ConfigManager.GetTemplateAsync(templateId) → AfdTemplate
  │
  ├─ 2. ReferenceDocBuilder.Build(refDocPath, template)
  │     → 生成含 AFD 样式的 reference.docx
  │
  ├─ 3. PandocPipeline.ToDocxAsync(md, rawDocx, refDoc)
  │     → Pandoc 使用 ref-doc 转换，得到"接近目标"的 DOCX
  │
  ├─ 4. OpenXmlStyleCorrector 三步修正
  │     ├─ ApplyAfdStyles(rawDocx, template)     — 逐段落/Run 精确修正
  │     ├─ ApplyPageSettings(rawDocx, defaults)  — 页面尺寸与边距
  │     └─ ApplyHeaderFooter(rawDocx, hf)        — 页眉页脚
  │
  ├─ 5. 输出（docx 直接复制 / pdf 再调 Pandoc）
  │
  └─ 6. 清理临时文件 → ConversionResult
```

## 4. 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `Pandoc/PandocPipeline.cs` | 实现 | CLI 封装，保持现有接口签名 |
| `Pandoc/OpenXmlStyleCorrector.cs` | 重写 | 合并 PageSettings/HeaderFooterBuilder，新增 ApplyPageSettings/ApplyHeaderFooter |
| `Pandoc/ReferenceDocBuilder.cs` | 新建 | 从 AfdTemplate 生成 reference.docx |
| `DocumentConversionEngine.cs` | 实现 | 编排全流程，注入 ConfigManager 依赖 |
| `Pandoc/PageSettings.cs` | 删除 | 逻辑合并到 OpenXmlStyleCorrector |
| `Pandoc/HeaderFooterBuilder.cs` | 删除 | 逻辑合并到 OpenXmlStyleCorrector |

## 5. PandocPipeline 设计

### 5.1 公开接口（保持不变）

```csharp
public class PandocPipeline
{
    public PandocPipeline(string? pandocPath = null);
    public Task<string> ToDocxAsync(string inputPath, string outputPath,
        string? referenceDoc = null, string? luaFilter = null,
        CancellationToken ct = default);
    public Task<string> ToPdfAsync(string inputPath, string outputPath,
        CancellationToken ct = default);
    public Task<string> ToAstJsonAsync(string inputPath,
        CancellationToken ct = default);
}
```

### 5.2 实现要点

- **构造函数**：默认 `_pandocPath = Path.Combine(AppContext.BaseDirectory, "tools", "pandoc-3.9.0.2", "pandoc.exe")`，支持自定义覆盖
- **RunAsync 私有方法**：封装 `Process.Start` + 重定向 stdout/stderr + `WaitForExitAsync` + 非零退出码时抛异常（携带 stderr 内容）
- **ToDocxAsync** 参数构建：
  - `-f markdown+tex_math_dollars+pipe_tables+raw_html`
  - `-t docx --standalone`
  - 可选 `--reference-doc` 和 `--lua-filter`
- **ToPdfAsync** 参数构建：
  - `--pdf-engine xelatex`
  - `-V CJKmainfont=宋体`
- **ToAstJsonAsync**：`-t json`，返回 stdout 字符串
- **参数转义**：路径含空格时用双引号包裹

## 6. ReferenceDocBuilder 设计

### 6.1 公开接口

```csharp
public static class ReferenceDocBuilder
{
    public static void Build(string outputPath, AfdTemplate template);
}
```

### 6.2 实现逻辑

1. `WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document)` 创建空文档
2. 添加 `MainDocumentPart` + `StyleDefinitionsPart`
3. 遍历 `template.Styles`：
   - 调用 `AfdStyleMapper.MapToOpenXmlStyleId(key)` 获取 styleId
   - 创建 `Style` 对象（类型 = Paragraph），设置 `StyleId`
   - 根据 AfdStyleDefinition 的字段填充 `StyleParagraphProperties` 和 `StyleRunProperties`
4. 将 `template.Defaults` 的页面尺寸/边距写入 `sectPr`
5. 保存并关闭

### 6.3 AFD 字段到 Style 属性映射

| AfdStyleDefinition 字段 | OpenXML 操作 |
|------------------------|-------------|
| `FontFamily` | `RunFonts { Ascii, EastAsia, HighAnsi }` |
| `FontSize` | `FontSize { Val = (pt * 2).ToString() }` |
| `Bold` | `Bold()` 自闭合元素 |
| `Italic` | `Italic()` 自闭合元素 |
| `Alignment` | `Justification { Val }` — left/center/right/both |
| `SpaceBefore` | `Spacing { Before = (pt * 20).ToString() }` |
| `SpaceAfter` | `Spacing { After = (pt * 20).ToString() }` |
| `LineSpacing` | `Spacing { Line = (倍数 * 240).ToString(), LineRule = Auto }` |
| `FirstLineIndent` | `Indentation { FirstLine = (pt * 20).ToString() }` |
| `HangingIndent` | `Indentation { Hanging = (pt * 20).ToString() }` |

## 7. OpenXmlStyleCorrector 设计（合并后）

### 7.1 公开接口（扩展）

```csharp
public static class OpenXmlStyleCorrector
{
    // 已有方法
    public static void ApplyAfdStyles(string docxPath, AfdTemplate template);
    public static void ApplyPageSettings(string docxPath, AfdDefaults defaults);
    public static void ApplyHeaderFooter(string docxPath, AfdHeaderFooter headerFooter);
}
```

### 7.2 ApplyAfdStyles 实现

1. 打开 DOCX（可读写模式）
2. 遍历 `Body` 下所有 `Paragraph`
3. 获取段落的 `ParagraphStyleId` → 通过 `AfdStyleMapper.MapToAfdStyleKey` 反查 AFD 键
4. 若找到匹配的 AFD 样式定义：
   - 修正 `ParagraphProperties`（对齐、间距、缩进）
   - 遍历段落内所有 `Run`，修正 `RunProperties`（字体、字号、粗体、斜体）
5. 对于 `Normal` 样式的段落，额外检查是否需要应用 body 样式（如首行缩进）

### 7.3 ApplyPageSettings 实现

1. 获取 `MainDocumentPart.Document.Body.SectionProperties`
2. 设置 `PageSize`：width/height 从 mm 转 twips (`mm * 567`)
3. 设置 `PageMargin`：top/bottom/left/right 从 mm 转 twips

### 7.4 ApplyHeaderFooter 实现

**页眉：**
1. 创建 `HeaderPart`，写入含指定文本/字体/字号/对齐的 `Paragraph`
2. 将 HeaderPart 关联到 `SectionProperties`

**页脚：**
1. 创建 `FooterPart`
2. 若 `PageNumbering = true`：插入页码域代码 (`fldChar` + `fldChar` + `pageNum`)
3. 设置起始页码（通过 `pgNumStart`）

## 8. DocumentConversionEngine 设计

### 8.1 公开接口（保持 stub 签名）

```csharp
public class DocumentConversionEngine
{
    public Task<ConversionResult> ConvertAsync(
        string markdownPath,
        string templateId,
        string outputFormat,
        CancellationToken ct = default);
}
```

### 8.2 依赖注入

```csharp
private readonly PandocPipeline _pandoc;
private readonly ConfigManager _configManager;

public DocumentConversionEngine(PandocPipeline pandoc, ConfigManager configManager)
{
    _pandoc = pandoc;
    _configManager = configManager;
}
```

### 8.3 实现流程

1. `template = await _configManager.GetTemplateAsync(templateId)`
   - null → 返回失败结果
2. 创建临时目录 `Path.Combine(Path.GetTempPath(), $"weavedoc-{Guid.NewGuid():N}")`
3. `ReferenceDocBuilder.Build(refDocPath, template)` 生成参考文档
4. `await _pandoc.ToDocxAsync(markdownPath, rawDocxPath, refDocPath, ct)` Pandoc 转换
5. 三步 OpenXML 修正
6. 输出处理：
   - "docx" → `File.Copy(rawDocxPath, outputPath, overwrite: true)`
   - "pdf" → `await _pandoc.ToPdfAsync(rawDocxPath, outputPath, ct)` （从修正后的 docx 转 PDF，保留 OpenXML 修正结果）
7. `finally` 块清理临时目录
8. 返回 `ConversionResult`

### 8.4 错误处理

- Pandoc 非零退出 → 捕获异常，返回 `ConversionResult { Success=false }`
- OpenXML 操作失败 → 捕获并包装
- 所有路径均确保临时目录被清理

## 9. 测试策略

### 9.1 PandocPipelineTests（xUnit）

| # | 测试方法 | 输入 | 预期结果 | 类型 |
|---|---------|------|---------|------|
| 1 | `ToDocxAsync_WithReferenceDoc_ProducesDocx` | 测试 MD + ref-doc | 输出 .docx 存在且可被 OpenXML 打开 | 集成 |
| 2 | `ToDocxAsync_InvalidInput_Throws` | 不存在的文件路径 | 抛异常（Pandoc 报错） | 单元 |
| 3 | `ToAstJsonAsync_ReturnsValidJson` | 测试 MD | 输出是合法 JSON 含 `blocks` 字段 | 集成 |
| 4 | `ReferenceDocBuilder_Build_CreatesValidDocx` | 含 heading1/body 样式的 AfdTemplate | 输出 docx 的 styles.xml 含 Heading1 和 Normal 样式定义 | 单元 |
| 5 | `OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx` | 含 Heading1 段落的 docx + AFD 模板 | Heading1 的字体变为黑体、字号变为 16pt | 单元 |
| 6 | `OpenXmlStyleCorrector_ApplyPageSettings_SetsDimensions` | A4 尺寸 AfdDefaults | docx 的 pgSz 为 11907x16839 twips | 单元 |
| 7 | `DocumentConversionEngine_FullPipeline_Succeeds` | MD 文件 + 模板 ID | ConversionResult.Success=true，输出 docx 可打开 | 集成 |

### 9.2 测试基础设施

- 测试 MD 文件内联创建（含标题、段落、数学公式、表格）
- AfdTemplate 测试对象直接构造（不依赖文件）
- 所有文件操作使用临时目录，测试后清理
- 集成测试标记 `[Fact]` 并可被 CI 过滤

## 10. 技术文档参考

本设计的实现细节参考了以下项目技术文档：

| 文档 | 参考章节 | 用途 |
|------|---------|------|
| [pandoc-reference.md](../../technical-reference/pandoc-reference.md) | 3 (CLI 用法)、4 (reference-doc 机制)、8 (C# 封装) | PandocPipeline 的参数构建、进程调用模式 |
| [openxml-pandoc-afd-reference.md](../../technical-reference/openxml-pandoc-afd-reference.md) | 2 (AFD Schema)、2.2 (映射表)、4 (端到端流程) | AFD→OpenXML 字段映射、ReferenceDocBuilder 生成逻辑 |
| [openxml-sdk-reference.md](../../technical-reference/openxml-sdk-reference.md) | 6-8 (样式操作、属性设置)、9 (单位换算) | OpenXmlStyleCorrector 的 API 用法、页面/字体/间距属性设置 |

---

## 11. 单位换算速查

| 转换 | 公式 | 示例 |
|------|------|------|
| pt → half-points | `pt * 2` | 12pt → 24 |
| pt → twips | `pt * 20` | 12pt → 240 |
| mm → twips | `mm * 567` | 30mm → 17010 |
| lineSpacing → w:line | `倍数 * 240` | 1.5x → 360 |
