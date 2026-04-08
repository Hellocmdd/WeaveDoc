# WeaveDoc.Converter.Tests

WeaveDoc.Converter 的单元测试项目，覆盖 AFD 模板解析、样式映射、Pandoc 管道、配置管理和 BibTeX 解析。

## 技术栈

- **xUnit 2** — 测试框架
- **Microsoft.NET.Test.Sdk 17** — 测试运行器
- **DocumentFormat.OpenXml 3.5.1** — Docx 验证（测试引用）
- **.NET 10**

## 测试文件

```
WeaveDoc.Converter.Tests/
├── AfdParserTests.cs          # AfdParser 解析与验证测试（8 个）
├── AfdStyleMapperTests.cs     # AfdStyleMapper 双向映射测试（15 个）
├── PandocPipelineTests.cs     # Pandoc 管道集成测试（7 个）
├── ConfigManagerTests.cs      # ConfigManager CRUD 测试（5 个）
├── BibtexParserTests.cs       # BibtexParser 解析测试（10 个）
└── WeaveDoc.Converter.Tests.csproj
```

## 测试概览

共 **47 个测试**，按模块分布：

| 测试类 | 数量 | 类型 | 说明 |
|--------|------|------|------|
| AfdParserTests | 8 | 单元测试 | JSON 解析、文件解析、模板验证、异常处理 |
| AfdStyleMapperTests | 15 | 单元测试 | AFD↔OpenXML 双向映射（`[InlineData]` 参数化） |
| PandocPipelineTests | 7 | 集成测试 | Pandoc CLI 调用、reference.docx 生成、OpenXML 样式修正、端到端管道 |
| ConfigManagerTests | 5 | 单元测试 | 模板保存/获取/列表/删除/覆盖，临时目录隔离 |
| BibtexParserTests | 10 | 单元测试 | 基础解析、多条目、嵌套括号、缩写展开、引号值、注释跳过、畸形容错 |

### 测试详情

#### AfdParserTests（8 个）

| 测试 | 验证内容 |
|------|---------|
| `Parse_ValidFile_ReturnsTemplate` | 从 JSON 文件解析出完整 AfdTemplate |
| `ParseJson_ValidJson_ReturnsTemplate` | 从 JSON 字符串解析 |
| `ParseJson_InvalidJson_ThrowsAfdParseException` | 无效 JSON 抛出自定义异常 |
| `Parse_NonexistentFile_ThrowsFileNotFoundException` | 文件不存在抛出 FileNotFoundException |
| `Validate_ValidTemplate_ReturnsTrue` | 合法模板通过验证 |
| `Validate_NullMeta_ThrowsAfdParseException` | meta 为空抛异常 |
| `Validate_EmptyTemplateName_ThrowsAfdParseException` | 模板名为空抛异常 |
| `Validate_EmptyStyles_ThrowsAfdParseException` | styles 为空抛异常 |
| `Validate_NegativeFontSize_ThrowsAfdParseException` | 字号 <= 0 抛异常 |
| `AfdParseException_CanBeThrownAndCaught` | 自定义异常基本行为 |
| `AfdParseException_CanWrapInnerException` | 异常包装内部异常 |

#### AfdStyleMapperTests（15 个）

通过 `[InlineData]` 参数化，测试所有 8 个已知映射的正向和反向查找，外加未知键的异常和 null 返回。

#### PandocPipelineTests（7 个）

集成测试，需要系统安装 Pandoc CLI：

| 测试 | 验证内容 |
|------|---------|
| `ToDocxAsync_WithInput_ProducesDocx` | Markdown → Docx 生成有效文件 |
| `ToDocxAsync_InvalidInput_ThrowsException` | 无效输入抛出异常 |
| `ToAstJsonAsync_ReturnsValidJson` | AST JSON 导出为有效 JSON |
| `ReferenceDocBuilder_Build_CreatesValidDocx` | AFD 模板 → reference.docx |
| `OpenXmlStyleCorrector_ApplyAfdStyles_ModifiesDocx` | 样式修正写入 Docx |
| `OpenXmlStyleCorrector_ApplyPageSettings_SetsDimensions` | 页面尺寸正确设置 |
| `FullPipeline_ReferenceDoc_ToDocx_StyleCorrection_ProducesValidDocx` | 端到端完整管道 |

#### ConfigManagerTests（5 个）

使用临时目录 + 内存 SQLite 隔离，每个测试独立：

| 测试 | 验证内容 |
|------|---------|
| `SaveAndGetTemplate_RoundTrips` | 保存后读取，数据完整一致 |
| `GetTemplate_NotExist_ReturnsNull` | 不存在的模板返回 null |
| `ListTemplates_ReturnsAll` | 列出所有已保存模板 |
| `DeleteTemplate_RemovesFromDbAndFile` | 删除后 DB 和文件都清除 |
| `SaveTemplate_OverwritesExisting` | 同 ID 保存两次，后者覆盖前者 |

#### BibtexParserTests（10 个）

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
# 运行全部测试
dotnet test tests/WeaveDoc.Converter.Tests -v n

# 运行指定模块
dotnet test tests/WeaveDoc.Converter.Tests --filter "BibtexParserTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "ConfigManagerTests" -v n
dotnet test tests/WeaveDoc.Converter.Tests --filter "PandocPipelineTests" -v n

# 不还原直接运行（开发迭代）
dotnet test tests/WeaveDoc.Converter.Tests --no-restore -v n
```

## 测试策略

- **隔离性**：ConfigManagerTests 使用 `Path.GetTempPath()` + GUID 创建临时目录，`IDisposable` 自动清理
- **集成测试**：PandocPipelineTests 调用真实 Pandoc CLI，验证端到端管道
- **容错测试**：BibtexParser 和 AfdParser 都包含畸形输入和边界条件的测试
- **TDD 流程**：ConfigManager 和 BibtexParser 模块采用测试驱动开发，先写失败测试再实现
