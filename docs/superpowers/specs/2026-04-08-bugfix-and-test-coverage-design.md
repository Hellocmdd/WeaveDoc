# Bug 修复 + 测试补充设计

> 日期：2026-04-08
> 状态：已批准

## 背景

生产就绪评估发现 2 个阻塞 Bug 和多处测试缺失。本次修复聚焦于使 Converter 库核心管道达到可信赖状态，不涉及 GUI/CLI 入口。

## Bug 修复

### Bug 1：根项目 WeaveDoc.csproj 构建失败（139 错误）

**原因**：SDK 风格项目默认包含目录树中所有 `*.cs` 文件。`WeaveDoc.csproj` 位于仓库根目录，将 `src/` 和 `tests/` 下的文件一并纳入编译，而根项目缺少 xUnit 等依赖导致 139 个编译错误。

**修复**：在 `WeaveDoc.csproj` 中排除子项目目录：

```xml
<ItemGroup>
  <Compile Remove="src\**\*.cs" />
  <Compile Remove="tests\**\*.cs" />
</ItemGroup>
```

### Bug 2：DocumentConversionEngine PDF 路径致命错误

**原因**：`ConvertAsync` 的 PDF 分支将 `rawDocxPath`（.docx 文件）传给 `ToPdfAsync`，但该方法使用 `-f markdown` 格式读取输入。Pandoc 会将 DOCX 当作 Markdown 解析，必然失败。

**位置**：`DocumentConversionEngine.cs:66`

**修复**：PDF 输出应从原始 Markdown 直接生成，不经过 DOCX 中间步骤：

```csharp
if (outputFormat == "pdf")
{
    await _pandoc.ToPdfAsync(markdownPath, outputPath, ct);
}
```

PDF 路径不经过 OpenXmlStyleCorrector（OpenXML 修正仅适用于 DOCX 格式）。

## 补充测试

### 测试 1：DocumentConversionEngine_ConvertAsync_Docx

端到端 DOCX 转换测试。流程：
1. 用 ConfigManager 保存测试模板
2. 创建临时 Markdown 文件
3. 调用 `ConvertAsync(mdPath, templateId, "docx")`
4. 验证 `ConversionResult.Success == true`
5. 验证输出文件存在且可被 OpenXML 打开

### 测试 2：DocumentConversionEngine_ConvertAsync_MissingTemplate

验证模板不存在时返回失败结果：
- `result.Success == false`
- `result.ErrorMessage` 包含模板名称

### 测试 3：DocumentConversionEngine_ConvertAsync_UnsupportedFormat

验证不支持格式（如 `"html"`）时返回失败：
- `result.Success == false`
- `result.ErrorMessage` 包含格式名称

### 测试 4：OpenXmlStyleCorrector_ApplyHeaderFooter

验证页眉页脚插入：
1. 创建测试 DOCX（含 SectionProperties）
2. 构造 `AfdHeaderFooter`（含页眉文本 + 页脚页码）
3. 调用 `ApplyHeaderFooter`
4. 重新打开验证：
   - HeaderPart 存在，包含指定文本和字体
   - FooterPart 存在，包含 PAGE 字段
   - HeaderReference / FooterReference 绑定到 SectionProperties

### 测试 5：OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage

验证起始页码设置：
1. 构造 `AfdFooterContent { StartPage = 3 }`
2. 调用 `ApplyHeaderFooter`
3. 验证 SectionProperties 包含 `PageNumberType { Start = 3 }`

## 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `WeaveDoc.csproj` | 修改 | 排除 `src/` 和 `tests/` 子目录 |
| `src/WeaveDoc.Converter/DocumentConversionEngine.cs` | 修改 | 修复 PDF 路径 Bug |
| `tests/WeaveDoc.Converter.Tests/PandocPipelineTests.cs` | 修改 | 补充 5 个测试 |

## 预期结果

- `dotnet build` 解决方案级别零错误
- 测试总数从 47 增至 52，全部通过
- `DocumentConversionEngine.ConvertAsync` 的 DOCX/PDF 路径均可正确工作
- `OpenXmlStyleCorrector.ApplyHeaderFooter` 有基本测试覆盖
