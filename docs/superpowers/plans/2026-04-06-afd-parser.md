# AFD 解析器实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 AFD 模板 JSON 解析、校验和 AFD↔OpenXML 样式键映射

**Architecture:** AfdParser 使用 System.Text.Json 反序列化 JSON → AfdTemplate record 对象，Validate 做基础结构校验（必填字段非空、类型正确），失败抛自定义 AfdParseException。AfdStyleMapper 用静态字典做双向映射。

**Tech Stack:** C# .NET 10, System.Text.Json (内置), xUnit 2.*, Microsoft.NET.Test.Sdk 17.*

---

## File Structure

| 文件 | 操作 | 职责 |
|------|------|------|
| `src/WeaveDoc.Converter/Afd/AfdParseException.cs` | **新建** | 自定义异常，封装解析/校验错误 |
| `src/WeaveDoc.Converter/Afd/AfdParser.cs` | **修改** (当前为骨架) | JSON → AfdTemplate 反序列化 + 结构校验 |
| `src/WeaveDoc.Converter/Afd/AfdStyleMapper.cs` | **修改** (当前为骨架) | AFD 样式键 ↔ OpenXML styleId 双向映射 |
| `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs` | **修改** (当前为空) | AfdParser 的 5 个测试用例 |
| `tests/WeaveDoc.Converter.Tests/AfdStyleMapperTests.cs` | **新建** | AfdStyleMapper 的 4 个测试用例 |

**已有模型文件（不修改）：**
- `src/WeaveDoc.Converter/Afd/Models/AfdTemplate.cs`
- `src/WeaveDoc.Converter/Afd/Models/AfdMeta.cs`
- `src/WeaveDoc.Converter/Afd/Models/AfdDefaults.cs`
- `src/WeaveDoc.Converter/Afd/Models/AfdStyleDefinition.cs`
- `src/WeaveDoc.Converter/Afd/Models/AfdHeaderFooter.cs`
- `src/WeaveDoc.Converter/Afd/Models/AfdNumbering.cs`

**测试依赖的样本文件（不修改）：**
- `src/WeaveDoc.Converter/Config/TemplateSchemas/default-thesis.json`

**项目文件（不修改）：**
- `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` — ImplicitUsings 已开启，System.Text.Json 自动引入
- `tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj` — 已引用 xUnit + Converter 项目

---

### Task 1: AfdParseException — 自定义异常类

**Files:**
- Create: `src/WeaveDoc.Converter/Afd/AfdParseException.cs`
- Test: `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs`

- [ ] **Step 1: 编写 AfdParseException 测试（验证异常可以被抛出和捕获）**

在 `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs` 中写入：

```csharp
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Tests;

public class AfdParserTests
{
    [Fact]
    public void AfdParseException_CanBeThrownAndCaught()
    {
        var ex = Assert.Throws<AfdParseException>(() =>
            throw new AfdParseException("test error"));

        Assert.Equal("test error", ex.Message);
    }

    [Fact]
    public void AfdParseException_CanWrapInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = Assert.Throws<AfdParseException>(() =>
            throw new AfdParseException("outer", inner));

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
```

- [ ] **Step 2: 运行测试确认失败（AfdParseException 类不存在）**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "AfdParserTests" --no-restore -v n`
Expected: 编译失败，AfdParseException 找不到

- [ ] **Step 3: 创建 AfdParseException.cs**

在 `src/WeaveDoc.Converter/Afd/AfdParseException.cs` 中写入：

```csharp
namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 模板解析或校验失败时抛出的异常
/// </summary>
public class AfdParseException : Exception
{
    public AfdParseException(string message) : base(message) { }

    public AfdParseException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "AfdParserTests" -v n`
Expected: 2 个测试全部 PASS

- [ ] **Step 5: 提交**

```bash
git add src/WeaveDoc.Converter/Afd/AfdParseException.cs tests/WeaveDoc.Converter.Tests/AfdParserTests.cs
git commit -m "feat(converter): add AfdParseException custom exception"
```

---

### Task 2: AfdParser.ParseJson — JSON 字符串反序列化

**Files:**
- Modify: `src/WeaveDoc.Converter/Afd/AfdParser.cs`
- Modify: `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs`

- [ ] **Step 1: 编写 ParseJson 测试（有效 JSON → 正确的 AfdTemplate）**

在 `AfdParserTests` 类中追加：

```csharp
[Fact]
public void ParseJson_ValidJson_ReturnsTemplate()
{
    var json = """
    {
      "meta": {
        "templateName": "测试模板",
        "version": "1.0.0",
        "author": "Tester",
        "description": "单元测试用"
      },
      "defaults": {
        "fontFamily": "宋体",
        "fontSize": 12,
        "lineSpacing": 1.5
      },
      "styles": {
        "heading1": {
          "displayName": "标题 1",
          "fontFamily": "黑体",
          "fontSize": 16,
          "bold": true,
          "alignment": "center"
        }
      }
    }
    """;

    var parser = new AfdParser();
    var result = parser.ParseJson(json);

    Assert.Equal("测试模板", result.Meta.TemplateName);
    Assert.Equal("1.0.0", result.Meta.Version);
    Assert.Equal("宋体", result.Defaults.FontFamily);
    Assert.Equal(12, result.Defaults.FontSize);
    Assert.Single(result.Styles);
    Assert.True(result.Styles["heading1"].Bold);
    Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
    Assert.Equal(16, result.Styles["heading1"].FontSize);
}
```

- [ ] **Step 2: 运行测试确认失败（ParseJson 抛 NotImplementedException）**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "ParseJson_ValidJson" -v n`
Expected: FAIL — NotImplementedException

- [ ] **Step 3: 实现 AfdParser.ParseJson**

将 `src/WeaveDoc.Converter/Afd/AfdParser.cs` 替换为：

```csharp
using System.Text.Json;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 样式解析器：将 JSON 模板文件解析为 AfdTemplate 对象
/// </summary>
public class AfdParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public AfdTemplate Parse(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"AFD 模板文件未找到: {jsonPath}", jsonPath);

        var content = File.ReadAllText(jsonPath);
        return ParseJson(content);
    }

    public AfdTemplate ParseJson(string jsonContent)
    {
        try
        {
            var template = JsonSerializer.Deserialize<AfdTemplate>(jsonContent, _options)
                ?? throw new AfdParseException("JSON 反序列化结果为 null");
            return template;
        }
        catch (JsonException ex)
        {
            throw new AfdParseException($"JSON 解析失败: {ex.Message}", ex);
        }
    }

    public bool Validate(AfdTemplate template) => throw new NotImplementedException();
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "ParseJson_ValidJson" -v n`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/WeaveDoc.Converter/Afd/AfdParser.cs tests/WeaveDoc.Converter.Tests/AfdParserTests.cs
git commit -m "feat(converter): implement AfdParser.ParseJson with System.Text.Json"
```

---

### Task 3: AfdParser.ParseJson — 无效 JSON 错误处理

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs`（追加测试）
- 无需修改 AfdParser.cs（Task 2 已实现）

- [ ] **Step 1: 编写无效 JSON 测试**

在 `AfdParserTests` 类中追加：

```csharp
[Fact]
public void ParseJson_InvalidJson_ThrowsAfdParseException()
{
    var parser = new AfdParser();
    var ex = Assert.Throws<AfdParseException>(() =>
        parser.ParseJson("{ invalid json !!!"));

    Assert.Contains("JSON 解析失败", ex.Message);
    Assert.NotNull(ex.InnerException);
}
```

- [ ] **Step 2: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "ParseJson_InvalidJson" -v n`
Expected: PASS（ParseJson 已在 Task 2 中处理了 JsonException → AfdParseException 包装）

- [ ] **Step 3: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/AfdParserTests.cs
git commit -m "test(converter): add ParseJson invalid JSON error handling test"
```

---

### Task 4: AfdParser.Parse — 从文件路径解析

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs`（追加测试）
- 无需修改 AfdParser.cs（Task 2 已实现）

- [ ] **Step 1: 编写 Parse 从文件解析测试**

在 `AfdParserTests` 类中追加：

```csharp
[Fact]
public void Parse_ValidFile_ReturnsTemplate()
{
    // 使用项目中的 default-thesis.json 作为测试文件
    var solutionRoot = FindSolutionRoot();
    var jsonPath = Path.Combine(solutionRoot,
        "src", "WeaveDoc.Converter", "Config", "TemplateSchemas", "default-thesis.json");

    var parser = new AfdParser();
    var result = parser.Parse(jsonPath);

    Assert.Equal("默认学术论文", result.Meta.TemplateName);
    Assert.Equal("WeaveDoc", result.Meta.Author);
    Assert.Equal("宋体", result.Defaults.FontFamily);
    Assert.Equal(12, result.Defaults.FontSize);
    Assert.Equal(1.5, result.Defaults.LineSpacing);
    Assert.NotNull(result.Defaults.PageSize);
    Assert.Equal(210, result.Defaults.PageSize.Width);
    Assert.Equal(297, result.Defaults.PageSize.Height);
    Assert.True(result.Styles.ContainsKey("heading1"));
    Assert.True(result.Styles.ContainsKey("heading2"));
    Assert.True(result.Styles.ContainsKey("body"));
    Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
    Assert.Equal(16, result.Styles["heading1"].FontSize);
    Assert.True(result.Styles["heading1"].Bold);
    Assert.Equal("center", result.Styles["heading1"].Alignment);
    Assert.Equal(24, result.Styles["body"].FirstLineIndent);
}

[Fact]
public void Parse_NonexistentFile_ThrowsFileNotFoundException()
{
    var parser = new AfdParser();
    Assert.Throws<FileNotFoundException>(() =>
        parser.Parse("/nonexistent/path/template.json"));
}

/// <summary>
/// 向上查找包含 .gitignore 的目录作为解决方案根目录
/// </summary>
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
```

- [ ] **Step 2: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "Parse_ValidFile|Parse_NonexistentFile" -v n`
Expected: 2 个测试全部 PASS

- [ ] **Step 3: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/AfdParserTests.cs
git commit -m "test(converter): add AfdParser.Parse file-based tests"
```

---

### Task 5: AfdParser.Validate — 结构校验

**Files:**
- Modify: `src/WeaveDoc.Converter/Afd/AfdParser.cs`（实现 Validate）
- Modify: `tests/WeaveDoc.Converter.Tests/AfdParserTests.cs`（追加测试）

- [ ] **Step 1: 编写 Validate 测试**

在 `AfdParserTests` 类中追加：

```csharp
[Fact]
public void Validate_ValidTemplate_ReturnsTrue()
{
    var template = new AfdTemplate
    {
        Meta = new AfdMeta { TemplateName = "测试" },
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["body"] = new AfdStyleDefinition { DisplayName = "正文" }
        }
    };

    var parser = new AfdParser();
    Assert.True(parser.Validate(template));
}

[Fact]
public void Validate_NullMeta_ThrowsAfdParseException()
{
    var template = new AfdTemplate
    {
        Meta = null!,
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["body"] = new AfdStyleDefinition()
        }
    };

    var parser = new AfdParser();
    var ex = Assert.Throws<AfdParseException>(() => parser.Validate(template));
    Assert.Contains("meta", ex.Message);
}

[Fact]
public void Validate_EmptyTemplateName_ThrowsAfdParseException()
{
    var template = new AfdTemplate
    {
        Meta = new AfdMeta { TemplateName = "" },
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["body"] = new AfdStyleDefinition()
        }
    };

    var parser = new AfdParser();
    var ex = Assert.Throws<AfdParseException>(() => parser.Validate(template));
    Assert.Contains("templateName", ex.Message);
}

[Fact]
public void Validate_EmptyStyles_ThrowsAfdParseException()
{
    var template = new AfdTemplate
    {
        Meta = new AfdMeta { TemplateName = "测试" },
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>()
    };

    var parser = new AfdParser();
    var ex = Assert.Throws<AfdParseException>(() => parser.Validate(template));
    Assert.Contains("styles", ex.Message);
}

[Fact]
public void Validate_NegativeFontSize_ThrowsAfdParseException()
{
    var template = new AfdTemplate
    {
        Meta = new AfdMeta { TemplateName = "测试" },
        Defaults = new AfdDefaults(),
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["body"] = new AfdStyleDefinition { FontSize = -1 }
        }
    };

    var parser = new AfdParser();
    var ex = Assert.Throws<AfdParseException>(() => parser.Validate(template));
    Assert.Contains("fontSize", ex.Message);
}
```

- [ ] **Step 2: 运行测试确认失败（Validate 抛 NotImplementedException）**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "Validate_" -v n`
Expected: FAIL — NotImplementedException

- [ ] **Step 3: 实现 AfdParser.Validate**

将 `AfdParser.cs` 中的 `Validate` 方法替换为：

```csharp
public bool Validate(AfdTemplate template)
{
    if (template.Meta is null)
        throw new AfdParseException("模板元信息 (meta) 不能为空");

    if (string.IsNullOrWhiteSpace(template.Meta.TemplateName))
        throw new AfdParseException("模板名称 (meta.templateName) 不能为空");

    if (template.Defaults is null)
        throw new AfdParseException("默认样式 (defaults) 不能为空");

    if (template.Styles is null || template.Styles.Count == 0)
        throw new AfdParseException("样式定义 (styles) 不能为空");

    foreach (var (key, style) in template.Styles)
    {
        if (style.FontSize is <= 0)
            throw new AfdParseException($"样式 '{key}' 的 fontSize 必须 > 0");
    }

    return true;
}
```

- [ ] **Step 4: 运行全部 Validate 测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "Validate_" -v n`
Expected: 5 个测试全部 PASS

- [ ] **Step 5: 将 Validate 集成到 Parse 流程中**

将 `AfdParser.cs` 中的 `Parse` 方法修改为解析后自动校验：

```csharp
public AfdTemplate Parse(string jsonPath)
{
    if (!File.Exists(jsonPath))
        throw new FileNotFoundException($"AFD 模板文件未找到: {jsonPath}", jsonPath);

    var content = File.ReadAllText(jsonPath);
    var template = ParseJson(content);
    Validate(template);
    return template;
}
```

注意：ParseJson 保持不变（只做反序列化，不做校验），Parse 在调用 ParseJson 后额外调用 Validate。

- [ ] **Step 6: 运行全部 AfdParser 测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "AfdParserTests" -v n`
Expected: 全部 PASS

- [ ] **Step 7: 提交**

```bash
git add src/WeaveDoc.Converter/Afd/AfdParser.cs tests/WeaveDoc.Converter.Tests/AfdParserTests.cs
git commit -m "feat(converter): implement AfdParser.Validate with structural checks"
```

---

### Task 6: AfdStyleMapper — 样式键双向映射

**Files:**
- Modify: `src/WeaveDoc.Converter/Afd/AfdStyleMapper.cs`
- Create: `tests/WeaveDoc.Converter.Tests/AfdStyleMapperTests.cs`

- [ ] **Step 1: 编写 AfdStyleMapper 测试**

创建 `tests/WeaveDoc.Converter.Tests/AfdStyleMapperTests.cs`：

```csharp
using WeaveDoc.Converter.Afd;

namespace WeaveDoc.Converter.Tests;

public class AfdStyleMapperTests
{
    [Theory]
    [InlineData("heading1", "Heading1")]
    [InlineData("heading2", "Heading2")]
    [InlineData("heading3", "Heading3")]
    [InlineData("body", "Normal")]
    [InlineData("caption", "Caption")]
    [InlineData("footnote", "FootnoteText")]
    [InlineData("reference", "Reference")]
    [InlineData("abstract", "Abstract")]
    public void MapToOpenXmlStyleId_KnownKey_ReturnsCorrectId(
        string afdKey, string expectedOpenXmlId)
    {
        Assert.Equal(expectedOpenXmlId, AfdStyleMapper.MapToOpenXmlStyleId(afdKey));
    }

    [Fact]
    public void MapToOpenXmlStyleId_UnknownKey_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            AfdStyleMapper.MapToOpenXmlStyleId("nonexistent_style"));
    }

    [Theory]
    [InlineData("Heading1", "heading1")]
    [InlineData("Heading2", "heading2")]
    [InlineData("Heading3", "heading3")]
    [InlineData("Normal", "body")]
    public void MapToAfdStyleKey_KnownId_ReturnsCorrectKey(
        string openXmlId, string expectedAfdKey)
    {
        Assert.Equal(expectedAfdKey, AfdStyleMapper.MapToAfdStyleKey(openXmlId));
    }

    [Fact]
    public void MapToAfdStyleKey_UnknownId_ReturnsNull()
    {
        Assert.Null(AfdStyleMapper.MapToAfdStyleKey("UnknownStyle"));
    }
}
```

- [ ] **Step 2: 运行测试确认失败（方法抛 NotImplementedException）**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "AfdStyleMapperTests" -v n`
Expected: FAIL — NotImplementedException

- [ ] **Step 3: 实现 AfdStyleMapper**

将 `src/WeaveDoc.Converter/Afd/AfdStyleMapper.cs` 替换为：

```csharp
using System.Linq;

namespace WeaveDoc.Converter.Afd;

/// <summary>
/// AFD 样式键 → OpenXML styleId 双向映射
/// </summary>
public static class AfdStyleMapper
{
    private static readonly Dictionary<string, string> _afdToOpenXml = new()
    {
        ["heading1"] = "Heading1",
        ["heading2"] = "Heading2",
        ["heading3"] = "Heading3",
        ["body"] = "Normal",
        ["caption"] = "Caption",
        ["footnote"] = "FootnoteText",
        ["reference"] = "Reference",
        ["abstract"] = "Abstract"
    };

    /// <summary>
    /// 将 AFD 样式键映射为 OpenXML styleId
    /// </summary>
    public static string MapToOpenXmlStyleId(string afdStyleKey)
    {
        return _afdToOpenXml.TryGetValue(afdStyleKey, out var openXmlId)
            ? openXmlId
            : throw new KeyNotFoundException(
                $"未找到 AFD 样式键 '{afdStyleKey}' 对应的 OpenXML styleId");
    }

    /// <summary>
    /// 将 OpenXML styleId 反向映射为 AFD 样式键
    /// </summary>
    public static string? MapToAfdStyleKey(string openXmlStyleId)
    {
        return _afdToOpenXml
            .FirstOrDefault(kvp => kvp.Value == openXmlStyleId)
            .Key;
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ --filter "AfdStyleMapperTests" -v n`
Expected: 全部 PASS（8 + 1 + 4 + 1 = 14 个测试断言）

- [ ] **Step 5: 提交**

```bash
git add src/WeaveDoc.Converter/Afd/AfdStyleMapper.cs tests/WeaveDoc.Converter.Tests/AfdStyleMapperTests.cs
git commit -m "feat(converter): implement AfdStyleMapper bidirectional mapping"
```

---

### Task 7: 全量测试验证 + 最终提交

**Files:**
- 无新增修改

- [ ] **Step 1: 运行全部测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/ -v n`
Expected: 全部 PASS（约 14 个测试用例）

- [ ] **Step 2: 验证项目编译无警告**

Run: `dotnet build src/WeaveDoc.Converter/ -v n`
Expected: 0 warnings, 0 errors

- [ ] **Step 3: 如果有未提交的改动，做最终提交**

Run: `git status` 检查。如有未跟踪/未提交文件，按需提交。

---

## Self-Review Checklist

- [x] **Spec coverage:** 设计文档中 AfdParser.Parse/ParseJson/Validate、AfdParseException、AfdStyleMapper 全部有对应 Task
- [x] **Placeholder scan:** 无 TBD/TODO/实现稍后，每步都有完整代码
- [x] **Type consistency:** `AfdParseException` 构造函数在 Task 1 定义，Task 3/5 引用一致；`AfdTemplate`/`AfdMeta`/`AfdDefaults`/`AfdStyleDefinition` 使用已有的 Models 文件
- [x] **Test data:** Parse 测试使用项目内置的 `default-thesis.json`，不依赖外部数据
