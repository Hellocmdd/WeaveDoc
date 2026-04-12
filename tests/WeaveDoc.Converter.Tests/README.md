# WeaveDoc.Converter.Tests

WeaveDoc.Converter 的单元测试和集成测试项目，覆盖 AFD 模板解析、样式映射、Pandoc 管道、配置管理、BibTeX 解析和端到端转换。

> **共 79 个测试，全部通过**

## 技术栈

- **xUnit 2** — 测试框架
- **Microsoft.NET.Test.Sdk 17** — 测试运行器
- **DocumentFormat.OpenXml 3.5.1** — Docx 结构验证
- **Microsoft.Data.Sqlite 10.0.5** — SQLite 临时数据库
- **.NET 10**

## 测试文件

```text
WeaveDoc.Converter.Tests/
├── AfdParserTests.cs          # AfdParser 解析与验证（13 个）
├── AfdStyleMapperTests.cs     # AfdStyleMapper 双向映射（5 个，含 33 条 InlineData）
├── PandocPipelineTests.cs     # Pandoc 管道 + OpenXML 修正 + 端到端（43 个）
├── ConfigManagerTests.cs      # ConfigManager CRUD + 种子模板（8 个）
├── BibtexParserTests.cs       # BibtexParser 解析全场景（10 个）
└── WeaveDoc.Converter.Tests.csproj
```

## 测试概览

| 测试类 | 数量 | 类型 | 说明 |
|--------|------|------|------|
| AfdParserTests | 13 | 单元测试 | JSON 解析、文件解析、模板验证、异常处理、三模板解析验证 |
| AfdStyleMapperTests | 5 | 单元测试 | AFD↔OpenXML 双向映射（`[Theory]` + `[InlineData]` 参数化，覆盖 14 个已知键 + 异常/null） |
| PandocPipelineTests | 43 | 集成测试 | Pandoc CLI 调用、reference.docx 生成、样式定义写入、冗余内联清除、页眉页脚、DOCX→PDF、3 模板端到端 |
| ConfigManagerTests | 8 | 单元测试 | 模板 CRUD、种子模板发现、幂等性 |
| BibtexParserTests | 10 | 单元测试 | 基础解析、多条目、嵌套括号、缩写展开、引号值、注释跳过、畸形容错 |

### AfdParserTests（13 个）

| 测试 | 验证内容 |
|------|---------|
| `Parse_ValidFile_ReturnsTemplate` | 从 JSON 文件解析出完整 AfdTemplate |
| `ParseJson_ValidJson_ReturnsTemplate` | 从 JSON 字符串解析 |
| `ParseJson_InvalidJson_ThrowsAfdParseException` | 无效 JSON 抛出自定义异常 |
| `Parse_NonexistentFile_ThrowsFileNotFoundException` | 文件不存在抛 FileNotFoundException |
| `Parse_CourseReportJson_ReturnsValidTemplate` | 课程报告模板解析验证 |
| `Parse_LabReportJson_ReturnsValidTemplate` | 实验报告模板解析验证 |
| `Validate_ValidTemplate_ReturnsTrue` | 合法模板通过验证 |
| `Validate_NullMeta_ThrowsAfdParseException` | meta 为空抛异常 |
| `Validate_EmptyTemplateName_ThrowsAfdParseException` | 模板名为空抛异常 |
| `Validate_EmptyStyles_ThrowsAfdParseException` | styles 为空抛异常 |
| `Validate_NegativeFontSize_ThrowsAfdParseException` | 字号 <= 0 抛异常 |
| `AfdParseException_CanBeThrownAndCaught` | 自定义异常基本行为 |
| `AfdParseException_CanWrapInnerException` | 异常包装内部异常 |

### AfdStyleMapperTests（5 个）

通过 `[Theory]` + `[InlineData]` 参数化，覆盖 h1-h6 + body/caption/footnote/reference/abstract/blockquote/list/codeblock 的正向映射、未知键异常、反向映射和未知 ID 返回 null。

### PandocPipelineTests（43 个）

集成测试，依赖项目构建时自动下载的 Pandoc CLI：

| 测试 | 验证内容 |
|------|---------|
| `ToDocxAsync_InvalidInput_ThrowsException` | 无效输入抛出异常 |
| `ToDocxAsync_WithInput_ProducesDocx` | Markdown → Docx 生成有效文件 |
| `ToAstJsonAsync_ReturnsValidJson` | AST JSON 导出为有效 JSON |
| `ReferenceDocBuilder_Build_CreatesValidDocx` | AFD → reference.docx（含 Heading1/Normal 样式定义验证） |
| `OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx` | 样式定义写入 styles.xml |
| `OpenXmlStyleCorrector_ApplyAfdStyles_WritesStyleDefinitions` | 验证 Heading1/Normal 样式定义含字体/字号/加粗/对齐 |
| `OpenXmlStyleCorrector_ApplyAfdStyles_StripsRedundantInline` | 冗余内联字体/字号被清除，用户有意加粗保留 |
| `OpenXmlStyleCorrector_ApplyAfdStyles_StylesTableCellParagraphs` | 表格内段落样式被写入样式定义 |
| `OpenXmlStyleCorrector_ApplyPageSettings_SetsDimensions` | 页面尺寸和边距正确设置 |
| `OpenXmlStyleCorrector_ApplyHeaderFooter` | 页眉页脚正确设置 |
| `OpenXmlStyleCorrector_ApplyHeaderFooter_StartPage` | 页码起始值正确设置 |
| `FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx` | default-thesis 端到端完整管道 |
| `FullPipeline_NewTemplate_ProducesValidDocx(default-thesis)` | 学术论文模板：页眉"学位论文"、页脚页码、样式定义、页边距 |
| `FullPipeline_NewTemplate_ProducesValidDocx(course-report)` | 课程报告模板端到端验证 |
| `FullPipeline_NewTemplate_ProducesValidDocx(lab-report)` | 实验报告模板端到端验证 |
| `DocumentConversionEngine_ConvertAsync_Docx` | DCE Docx 端到端转换（含样式定义验证） |
| `DocumentConversionEngine_ConvertAsync_MissingTemplate` | 模板不存在时返回错误 |
| `DocumentConversionEngine_ConvertAsync_UnsupportedFormat` | 不支持的格式返回错误 |
| `DocumentConversionEngine_ConvertAsync_Pdf` | DCE PDF 端到端 |
| `FromDocxToPdfAsync_WithDocxInput_ProducesPdf` | DOCX → PDF 生成有效 PDF 文件 |
| `ToDocxAsync_Blockquote_AppliesBlockquoteStyle` | Lua Filter 注入 Blockquote 样式 |
| `ToDocxAsync_CodeBlock_AppliesCodeBlockStyle` | Lua Filter 注入 CodeBlock 样式 |

### ConfigManagerTests（8 个）

使用临时目录 + SQLite 隔离，每个测试独立清理：

| 测试 | 验证内容 |
|------|---------|
| `SaveAndGetTemplate_RoundTrips` | 保存后读取，数据完整一致 |
| `GetTemplate_NotExist_ReturnsNull` | 不存在的模板返回 null |
| `ListTemplates_ReturnsAll` | 列出所有已保存模板 |
| `DeleteTemplate_RemovesFromDbAndFile` | 删除后 DB 和文件都清除 |
| `SaveTemplate_OverwritesExisting` | 同 ID 保存两次，后者覆盖前者 |
| `EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates` | 空库种子发现所有内置模板 |
| `EnsureSeedTemplatesAsync_SkipsExistingTemplates` | 已存在的模板不重复导入 |
| `EnsureSeedTemplatesAsync_Idempotent` | 重复调用幂等 |

### BibtexParserTests（10 个）

| 测试 | 验证内容 |
|------|---------|
| `Parse_BasicArticle_ExtractsFields` | 基础 @article 字段提取 |
| `Parse_MultipleEntries_ReturnsAll` | 多条目（article/book/inproceedings） |
| `Parse_EmptyInput_ReturnsEmptyList` | 空输入返回空列表 |
| `ParseSingle_ReturnsFirstEntry` | 返回第一条解析结果 |
| `ParseSingle_NoEntry_ReturnsNull` | 无条目返回 null |
| `Parse_NestedBraces_ExtractsCorrectly` | 嵌套 `{...{...}...}` 提取 |
| `Parse_StringAbbreviation_ExpandsValue` | `@string{jan="January"}` 缩写展开 |
| `Parse_QuotedValues_ExtractsCorrectly` | 双引号 `"..."` 值提取 |
| `Parse_SkipsCommentAndPreamble` | 跳过 `@comment` 和 `@preamble` |
| `Parse_MalformedEntry_SilentlySkips` | 无逗号畸形条目静默跳过 |

## 运行测试

```bash
# 运行全部测试（79 个）
dotnet test tests/WeaveDoc.Converter.Tests -v n

# 运行指定模块
dotnet test tests/WeaveDoc.Converter.Tests --filter "AfdParserTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "AfdStyleMapperTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "ConfigManagerTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "BibtexParserTests" -v n

# 不还原直接运行（开发迭代）
dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n
```

## 测试策略

- **隔离性**：ConfigManagerTests 使用 `Path.GetTempPath()` + GUID 创建临时目录和 SQLite 数据库，`try/finally` 自动清理
- **真实集成测试**：PandocPipelineTests 调用真实 Pandoc CLI，验证完整管道链路
- **样式定义验证**：所有样式相关测试验证 `StyleDefinitionsPart` 中的 `StyleRunProperties`，而非内联 `RunProperties`
- **容错测试**：BibtexParser 和 AfdParser 都包含畸形输入和边界条件测试
- **幂等性测试**：种子模板发现操作验证重复调用安全性
- **参数化端到端**：`FullPipeline_NewTemplate_ProducesValidDocx` 使用 `[Theory]` 覆盖全部 3 个内置模板
