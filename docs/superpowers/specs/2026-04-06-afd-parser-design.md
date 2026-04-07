# AFD 解析器设计文档

> **模块**: 3.1 AFD 样式解析器
> **日期**: 2026-04-06
> **状态**: 已批准
> **涉及文件**: `AfdParser.cs`, `AfdStyleMapper.cs`, `AfdParseException.cs`, `AfdParserTests.cs`, `AfdStyleMapperTests.cs`

---

## 1. 概述

实现 AFD（Academic Format Definition）模板的 JSON 解析和校验功能。解析器将 JSON 模板文件转换为 `AfdTemplate` 对象，并提供基础的 AFD 样式键到 OpenXML styleId 的映射。

## 2. 技术选型

| 项目 | 选择 | 理由 |
|------|------|------|
| JSON 库 | System.Text.Json | .NET 内置，零依赖，对 record 类型支持完善 |
| 校验级别 | 基础结构校验 | 只检查必填字段和类型，业务规则留上层 |
| 错误报告 | bool + 抛异常 | 接口简单直接 |
| 映射实现 | 静态 Dictionary | 映射关系固定，一行加一个 |

## 3. AfdParser 设计

### 3.1 公开接口

```csharp
public class AfdParser
{
    public AfdTemplate Parse(string jsonPath);
    public AfdTemplate ParseJson(string jsonContent);
    public bool Validate(AfdTemplate template);
}
```

### 3.2 JsonSerializerOptions

```csharp
private static readonly JsonSerializerOptions _options = new()
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};
```

- `PropertyNameCaseInsensitive`：JSON 使用 camelCase，C# record 使用 PascalCase
- `ReadCommentHandling`/`AllowTrailingCommas`：提升 JSON 文件容错性

### 3.3 Parse 流程

```
Parse(jsonPath)
  → File.ReadAllText(jsonPath)
  → ParseJson(content)
  → Validate(result)
  → return result
```

### 3.4 Validate 校验规则

| 校验项 | 条件 | 失败消息 |
|--------|------|---------|
| Meta | `template.Meta != null` | "模板元信息 (meta) 不能为空" |
| TemplateName | `!string.IsNullOrWhiteSpace(template.Meta.TemplateName)` | "模板名称 (meta.templateName) 不能为空" |
| Defaults | `template.Defaults != null` | "默认样式 (defaults) 不能为空" |
| Styles | `template.Styles != null && template.Styles.Count > 0` | "样式定义 (styles) 不能为空" |
| FontSize | 每个 style 的 FontSize > 0（如果存在） | "样式 '{key}' 的 fontSize 必须 > 0" |

校验失败时抛出 `AfdParseException`。

### 3.5 错误处理

- `JsonException` → 包装为 `AfdParseException("JSON 解析失败: {originalMessage}")`
- 文件不存在 → `FileNotFoundException`
- 校验失败 → `AfdParseException` 携带具体字段和原因

## 4. AfdParseException 设计

```csharp
public class AfdParseException : Exception
{
    public AfdParseException(string message) : base(message) { }
    public AfdParseException(string message, Exception inner) : base(message, inner) { }
}
```

文件位置：`src/WeaveDoc.Converter/Afd/AfdParseException.cs`

## 5. AfdStyleMapper 设计

### 5.1 映射表

```csharp
private static readonly Dictionary<string, string> _afdToOpenXml = new()
{
    ["heading1"] = "Heading1",
    ["heading2"] = "Heading2",
    ["heading3"] = "Heading3",
    ["body"]     = "Normal",
    ["caption"]  = "Caption",
    ["footnote"] = "FootnoteText",
    ["reference"] = "Reference",
    ["abstract"] = "Abstract"
};
```

### 5.2 方法行为

- `MapToOpenXmlStyleId(key)` — 查字典，找不到抛 `KeyNotFoundException`
- `MapToAfdStyleKey(styleId)` — 反查字典值，找不到返回 `null`

## 6. 单元测试

### 6.1 AfdParser 测试（xUnit）

| # | 测试方法 | 输入 | 预期结果 |
|---|---------|------|---------|
| 1 | `Parse_ValidFile_ReturnsTemplate` | `default-thesis.json` | 成功，Meta/Defaults/Styles 各字段正确 |
| 2 | `Parse_NonexistentFile_ThrowsFileNotFoundException` | 不存在的路径 | 抛 FileNotFoundException |
| 3 | `ParseJson_InvalidJson_ThrowsAfdParseException` | `"{ invalid"` | 抛 AfdParseException |
| 4 | `Validate_EmptyStyles_ThrowsAfdParseException` | Styles 为空字典 | 抛 AfdParseException |
| 5 | `Validate_NullMeta_ThrowsAfdParseException` | Meta = null | 抛 AfdParseException |

### 6.2 AfdStyleMapper 测试

| # | 测试方法 | 输入 | 预期结果 |
|---|---------|------|---------|
| 6 | `MapToOpenXmlStyleId_KnownKey` | `"heading1"` | `"Heading1"` |
| 7 | `MapToOpenXmlStyleId_UnknownKey` | `"unknown"` | 抛 KeyNotFoundException |
| 8 | `MapToAfdStyleKey_KnownId` | `"Heading1"` | `"heading1"` |
| 9 | `MapToAfdStyleKey_UnknownId` | `"UnknownStyle"` | `null` |

测试使用临时 JSON 文件，测试结束后清理。

## 7. 文件清单

| 文件 | 操作 | 代码行估计 |
|------|------|-----------|
| `src/.../Afd/AfdParser.cs` | 实现 | ~60 行 |
| `src/.../Afd/AfdStyleMapper.cs` | 实现 | ~30 行 |
| `src/.../Afd/AfdParseException.cs` | 新建 | ~10 行 |
| `tests/.../AfdParserTests.cs` | 实现 | ~80 行 |
| `tests/.../AfdStyleMapperTests.cs` | 新建 | ~40 行 |
