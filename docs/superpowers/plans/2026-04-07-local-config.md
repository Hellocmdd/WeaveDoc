# 本地配置管理（Task 3.3）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 WeaveDoc 本地配置管理模块——模板库 CRUD（SQLite 元信息 + JSON 文件）和 BibTeX 文献解析。

**Architecture:** 分层职责：TemplateRepository（internal，SQLite 元信息 CRUD）→ ConfigManager（public，编排 Repository + AfdParser + 文件管理）→ DocumentConversionEngine 消费。BibtexParser 独立，纯文本解析无外部依赖。

**Tech Stack:** C# .NET 10, Microsoft.Data.Sqlite, System.Text.Json, xUnit

---

## File Structure

| 文件 | 职责 | 操作 |
|------|------|------|
| `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` | 添加 Microsoft.Data.Sqlite 包 | 修改 |
| `src/WeaveDoc.Converter/Config/TemplateRepository.cs` | SQLite 元信息 CRUD（internal） | 重写 |
| `src/WeaveDoc.Converter/Config/ConfigManager.cs` | 公共 API：模板库 CRUD + 文件管理 | 重写 |
| `src/WeaveDoc.Converter/Config/BibtexParser.cs` | BibTeX 文本解析 | 重写 |
| `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` | ConfigManager CRUD 测试 | 重写 |
| `tests/WeaveDoc.Converter.Tests/BibtexParserTests.cs` | BibtexParser 解析测试 | 新建 |

---

## Task 1: 添加 NuGet 依赖

**Files:**
- Modify: `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`

- [ ] **Step 1: 添加 Microsoft.Data.Sqlite 包**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet add src/WeaveDoc.Converter/WeaveDoc.Converter.csproj package Microsoft.Data.Sqlite`

- [ ] **Step 2: 验证构建通过**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/WeaveDoc.Converter/WeaveDoc.Converter.csproj
git commit -m "chore(converter): add Microsoft.Data.Sqlite package for Task 3.3"
```

---

## Task 2: 实现 TemplateRepository

**Files:**
- Rewrite: `src/WeaveDoc.Converter/Config/TemplateRepository.cs`

- [ ] **Step 1: 实现 TemplateRepository**

Replace entire content of `src/WeaveDoc.Converter/Config/TemplateRepository.cs` with:

```csharp
using Microsoft.Data.Sqlite;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 模板仓储：SQLite 存储 AFD 模板元信息和文件路径
/// </summary>
internal class TemplateRepository
{
    private readonly string _dbPath;
    private bool _initialized;

    public TemplateRepository(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS templates (
                template_id  TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                version      TEXT NOT NULL,
                author       TEXT NOT NULL,
                description  TEXT NOT NULL,
                json_path    TEXT NOT NULL,
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL
            )
            """;
        await cmd.ExecuteNonQueryAsync();
        _initialized = true;
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
            await InitializeAsync();
    }

    public async Task<AfdMeta?> GetMetaAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_id, name, version, author, description, json_path
            FROM templates WHERE template_id = @id
            """;
        cmd.Parameters.AddWithValue("@id", templateId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AfdMeta
            {
                TemplateName = reader.GetString(1),
                Version = reader.GetString(2),
                Author = reader.GetString(3),
                Description = reader.GetString(4)
            };
        }

        return null;
    }

    public async Task<string?> GetJsonPathAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT json_path FROM templates WHERE template_id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<List<AfdMeta>> GetAllMetasAsync()
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT template_id, name, version, author, description
            FROM templates ORDER BY name
            """;

        var metas = new List<AfdMeta>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            metas.Add(new AfdMeta
            {
                TemplateName = reader.GetString(1),
                Version = reader.GetString(2),
                Author = reader.GetString(3),
                Description = reader.GetString(4)
            });
        }

        return metas;
    }

    public async Task UpsertAsync(string templateId, AfdMeta meta, string jsonPath)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var now = DateTime.UtcNow.ToString("o");
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO templates
                (template_id, name, version, author, description, json_path, created_at, updated_at)
            VALUES
                (@id, @name, @version, @author, @description, @jsonPath,
                 COALESCE((SELECT created_at FROM templates WHERE template_id = @id), @now), @now)
            """;
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@name", meta.TemplateName);
        cmd.Parameters.AddWithValue("@version", meta.Version);
        cmd.Parameters.AddWithValue("@author", meta.Author);
        cmd.Parameters.AddWithValue("@description", meta.Description);
        cmd.Parameters.AddWithValue("@jsonPath", jsonPath);
        cmd.Parameters.AddWithValue("@now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string templateId)
    {
        await EnsureInitializedAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM templates WHERE template_id = @id";
        cmd.Parameters.AddWithValue("@id", templateId);

        await cmd.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 2: 验证构建通过**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/WeaveDoc.Converter/Config/TemplateRepository.cs
git commit -m "feat(config): implement TemplateRepository with SQLite metadata storage"
```

---

## Task 3: 实现 ConfigManager

**Files:**
- Rewrite: `src/WeaveDoc.Converter/Config/ConfigManager.cs`

- [ ] **Step 1: 实现 ConfigManager**

Replace entire content of `src/WeaveDoc.Converter/Config/ConfigManager.cs` with:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;

namespace WeaveDoc.Converter.Config;

/// <summary>
/// 本地配置管理：模板库 CRUD、配置读写
/// </summary>
public class ConfigManager
{
    private readonly TemplateRepository _repository;
    private readonly AfdParser _parser;
    private readonly string _templatesDir;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public ConfigManager(string dbPath)
    {
        _repository = new TemplateRepository(dbPath);
        _parser = new AfdParser();
        _templatesDir = Path.Combine(
            Path.GetDirectoryName(dbPath) ?? ".", "templates");
    }

    public async Task<AfdTemplate?> GetTemplateAsync(string templateId)
    {
        var jsonPath = await _repository.GetJsonPathAsync(templateId);
        if (jsonPath == null || !File.Exists(jsonPath))
            return null;

        return _parser.Parse(jsonPath);
    }

    public async Task<List<AfdMeta>> ListTemplatesAsync()
    {
        return await _repository.GetAllMetasAsync();
    }

    public async Task SaveTemplateAsync(string templateId, AfdTemplate template)
    {
        Directory.CreateDirectory(_templatesDir);

        var jsonPath = Path.Combine(_templatesDir, $"{templateId}.json");
        var json = JsonSerializer.Serialize(template, _jsonOptions);
        await File.WriteAllTextAsync(jsonPath, json);

        await _repository.UpsertAsync(templateId, template.Meta, jsonPath);
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        var jsonPath = await _repository.GetJsonPathAsync(templateId);

        await _repository.DeleteAsync(templateId);

        if (jsonPath != null && File.Exists(jsonPath))
            File.Delete(jsonPath);
    }
}
```

- [ ] **Step 2: 验证构建通过**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/WeaveDoc.Converter/Config/ConfigManager.cs
git commit -m "feat(config): implement ConfigManager with template CRUD and JSON file management"
```

---

## Task 4: 实现 BibtexParser

**Files:**
- Rewrite: `src/WeaveDoc.Converter/Config/BibtexParser.cs`

- [ ] **Step 1: 实现 BibtexParser**

Replace entire content of `src/WeaveDoc.Converter/Config/BibtexParser.cs` with:

```csharp
namespace WeaveDoc.Converter.Config;

/// <summary>
/// BibTeX 文献解析器（实用级）
/// </summary>
public class BibtexParser
{
    public List<BibtexEntry> Parse(string bibContent)
    {
        var entries = new List<BibtexEntry>();
        if (string.IsNullOrWhiteSpace(bibContent))
            return entries;

        var abbreviations = ParseStringDefinitions(bibContent);
        var i = 0;

        while (i < bibContent.Length)
        {
            // 找到下一个 @
            var atIndex = bibContent.IndexOf('@', i);
            if (atIndex < 0) break;

            // 读取 entry type
            var typeStart = atIndex + 1;
            var typeEnd = typeStart;
            while (typeEnd < bibContent.Length && char.IsLetter(bibContent[typeEnd]))
                typeEnd++;

            var entryType = bibContent[typeStart..typeEnd].ToLowerInvariant();

            // 跳过 @comment 和 @preamble
            if (entryType is "comment" or "preamble")
            {
                i = typeEnd;
                continue;
            }

            // 跳过 @string（已处理）
            if (entryType == "string")
            {
                i = typeEnd;
                continue;
            }

            // 找到开括号
            var openBrace = bibContent.IndexOf('{', typeEnd);
            if (openBrace < 0) break;

            // 用括号计数器找到匹配的闭括号
            var closeBrace = FindMatchingBrace(bibContent, openBrace);
            if (closeBrace < 0) break;

            var body = bibContent[(openBrace + 1)..closeBrace];
            var entry = ParseEntryBody(entryType, body, abbreviations);
            if (entry != null)
                entries.Add(entry);

            i = closeBrace + 1;
        }

        return entries;
    }

    public BibtexEntry? ParseSingle(string entryText)
    {
        var entries = Parse(entryText);
        return entries.Count > 0 ? entries[0] : null;
    }

    private Dictionary<string, string> ParseStringDefinitions(string content)
    {
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < content.Length)
        {
            var atIndex = content.IndexOf("@string", i, StringComparison.OrdinalIgnoreCase);
            if (atIndex < 0) break;

            var openBrace = content.IndexOf('{', atIndex);
            if (openBrace < 0) break;

            var closeBrace = FindMatchingBrace(content, openBrace);
            if (closeBrace < 0) break;

            var body = content[(openBrace + 1)..closeBrace].Trim();
            var eqIndex = body.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = body[..eqIndex].Trim();
                var valuePart = body[(eqIndex + 1)..].Trim();
                var value = ExtractFieldValue(valuePart, abbreviations);
                if (value != null)
                    abbreviations[key] = value;
            }

            i = closeBrace + 1;
        }

        return abbreviations;
    }

    private static int FindMatchingBrace(string text, int openPos)
    {
        var depth = 0;
        for (var i = openPos; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private BibtexEntry? ParseEntryBody(string entryType, string body,
        Dictionary<string, string> abbreviations)
    {
        // 提取 citation key：逗号前的第一个 token
        var commaIndex = body.IndexOf(',');
        if (commaIndex < 0) return null;

        var citationKey = body[..commaIndex].Trim();
        if (string.IsNullOrEmpty(citationKey)) return null;

        var fieldsText = body[(commaIndex + 1)..];
        var fields = ParseFields(fieldsText, abbreviations);

        return new BibtexEntry
        {
            EntryType = entryType,
            CitationKey = citationKey,
            Fields = fields
        };
    }

    private Dictionary<string, string> ParseFields(string text,
        Dictionary<string, string> abbreviations)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < text.Length)
        {
            // 跳过空白和逗号
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ','))
                i++;

            if (i >= text.Length) break;

            // 读取字段名
            var nameStart = i;
            while (i < text.Length && text[i] != '=' && !char.IsWhiteSpace(text[i]))
                i++;

            var fieldName = text[nameStart..i].Trim();
            if (string.IsNullOrEmpty(fieldName)) break;

            // 跳过空白到 =
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '='))
                i++;

            if (i >= text.Length) break;

            // 提取值
            var value = ExtractFieldValue(text[i..], abbreviations, out var consumed);
            if (value != null)
                fields[fieldName] = value;

            i += consumed;
        }

        return fields;
    }

    private string? ExtractFieldValue(string text,
        Dictionary<string, string> abbreviations)
    {
        return ExtractFieldValue(text, abbreviations, out _);
    }

    private string? ExtractFieldValue(string text,
        Dictionary<string, string> abbreviations, out int consumed)
    {
        consumed = 0;
        if (string.IsNullOrEmpty(text)) return null;

        var trimmed = text.TrimStart();
        var leadingWhitespace = text.Length - trimmed.Length;

        if (trimmed.Length == 0) return null;

        if (trimmed[0] == '{')
        {
            // 大括号值
            var closeBrace = FindMatchingBrace(trimmed, 0);
            if (closeBrace < 0) return null;

            consumed = leadingWhitespace + closeBrace + 1;
            return trimmed[1..closeBrace];
        }

        if (trimmed[0] == '"')
        {
            // 引号值
            var closeQuote = trimmed.IndexOf('"', 1);
            if (closeQuote < 0) return null;

            consumed = leadingWhitespace + closeQuote + 1;
            return trimmed[1..closeQuote];
        }

        // 裸字（可能带 # 拼接）
        var end = 0;
        while (end < trimmed.Length &&
               trimmed[end] != ',' && trimmed[end] != '}' &&
               !char.IsWhiteSpace(trimmed[end]))
            end++;

        if (end == 0) return null;

        consumed = leadingWhitespace + end;
        var bareWord = trimmed[..end].Trim();

        // 查找缩写映射
        return abbreviations.TryGetValue(bareWord, out var expanded)
            ? expanded : bareWord;
    }
}

public record BibtexEntry
{
    public string EntryType { get; init; } = "";     // article, book, inproceedings 等
    public string CitationKey { get; init; } = "";    // @article{this_part,
    public Dictionary<string, string> Fields { get; init; } = new();
}
```

- [ ] **Step 2: 验证构建通过**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet build src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/WeaveDoc.Converter/Config/BibtexParser.cs
git commit -m "feat(config): implement BibtexParser with string abbreviation and nested brace support"
```

---

## Task 5: 编写 ConfigManager 测试

**Files:**
- Rewrite: `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs`

- [ ] **Step 1: 编写 ConfigManager CRUD 测试**

Replace entire content of `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` with:

```csharp
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _manager;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _manager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static AfdTemplate CreateTestTemplate(string name = "测试模板") => new()
    {
        Meta = new AfdMeta
        {
            TemplateName = name,
            Version = "1.0.0",
            Author = "Test",
            Description = "测试用模板"
        },
        Defaults = new AfdDefaults
        {
            FontFamily = "宋体",
            FontSize = 12,
            LineSpacing = 1.5
        },
        Styles = new Dictionary<string, AfdStyleDefinition>
        {
            ["heading1"] = new()
            {
                DisplayName = "标题 1",
                FontFamily = "黑体",
                FontSize = 16,
                Bold = true
            }
        }
    };

    [Fact]
    public async Task SaveAndGetTemplate_RoundTrips()
    {
        var template = CreateTestTemplate();

        await _manager.SaveTemplateAsync("test-tpl", template);
        var result = await _manager.GetTemplateAsync("test-tpl");

        Assert.NotNull(result);
        Assert.Equal("测试模板", result.Meta.TemplateName);
        Assert.Equal("黑体", result.Styles["heading1"].FontFamily);
    }

    [Fact]
    public async Task GetTemplate_NotExist_ReturnsNull()
    {
        var result = await _manager.GetTemplateAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task ListTemplates_ReturnsAll()
    {
        await _manager.SaveTemplateAsync("tpl-a", CreateTestTemplate("模板A"));
        await _manager.SaveTemplateAsync("tpl-b", CreateTestTemplate("模板B"));

        var list = await _manager.ListTemplatesAsync();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, m => m.TemplateName == "模板A");
        Assert.Contains(list, m => m.TemplateName == "模板B");
    }

    [Fact]
    public async Task DeleteTemplate_RemovesFromDbAndFile()
    {
        await _manager.SaveTemplateAsync("to-delete", CreateTestTemplate());
        var before = await _manager.GetTemplateAsync("to-delete");
        Assert.NotNull(before);

        await _manager.DeleteTemplateAsync("to-delete");

        var after = await _manager.GetTemplateAsync("to-delete");
        Assert.Null(after);
    }

    [Fact]
    public async Task SaveTemplate_OverwritesExisting()
    {
        await _manager.SaveTemplateAsync("overwrite", CreateTestTemplate("V1"));
        await _manager.SaveTemplateAsync("overwrite", CreateTestTemplate("V2"));

        var result = await _manager.GetTemplateAsync("overwrite");
        Assert.NotNull(result);
        Assert.Equal("V2", result.Meta.TemplateName);
    }
}
```

- [ ] **Step 2: 运行测试**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet test tests/WeaveDoc.Converter.Tests --filter "ConfigManagerTests" --no-restore -v n`
Expected: 5 tests PASS

- [ ] **Step 3: Commit**

```bash
git add tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs
git commit -m "test(config): add ConfigManager CRUD tests with real SQLite"
```

---

## Task 6: 编写 BibtexParser 测试

**Files:**
- Create: `tests/WeaveDoc.Converter.Tests/BibtexParserTests.cs`

- [ ] **Step 1: 编写 BibtexParser 测试**

Create `tests/WeaveDoc.Converter.Tests/BibtexParserTests.cs` with:

```csharp
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Tests;

public class BibtexParserTests
{
    [Fact]
    public void Parse_BasicArticle_ExtractsFields()
    {
        var bib = """
            @article{smith2024,
              author = {John Smith and Jane Doe},
              title = {A Study on AI},
              journal = {Nature},
              year = {2024},
              volume = {10},
              pages = {1--20}
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        var entry = entries[0];
        Assert.Equal("article", entry.EntryType);
        Assert.Equal("smith2024", entry.CitationKey);
        Assert.Equal("John Smith and Jane Doe", entry.Fields["author"]);
        Assert.Equal("A Study on AI", entry.Fields["title"]);
        Assert.Equal("2024", entry.Fields["year"]);
    }

    [Fact]
    public void Parse_NestedBraces_ExtractsCorrectly()
    {
        var bib = """
            @article{nested,
              title = {A {Very {Nested} Title} Here}
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("A {Very {Nested} Title} Here", entries[0].Fields["title"]);
    }

    [Fact]
    public void Parse_StringAbbreviation_ExpandsValue()
    {
        var bib = """
            @string{jan = "January"}

            @article{abbrev,
              author = {Test Author},
              month = jan
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("January", entries[0].Fields["month"]);
    }

    [Fact]
    public void Parse_QuotedValues_ExtractsCorrectly()
    {
        var bib = """
            @book{book1,
              title = "A Book Title",
              publisher = "Oxford University Press"
            }
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("A Book Title", entries[0].Fields["title"]);
        Assert.Equal("Oxford University Press", entries[0].Fields["publisher"]);
    }

    [Fact]
    public void Parse_MultipleEntries_ReturnsAll()
    {
        var bib = """
            @article{first, title = {First}}
            @book{second, title = {Second}}
            @inproceedings{third, title = {Third}}
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Equal(3, entries.Count);
        Assert.Equal("first", entries[0].CitationKey);
        Assert.Equal("second", entries[1].CitationKey);
        Assert.Equal("third", entries[2].CitationKey);
    }

    [Fact]
    public void Parse_SkipsCommentAndPreamble()
    {
        var bib = """
            @comment{Some comment}
            @preamble{Some preamble}
            @article{kept, title = {Kept}}
            """;

        var entries = new BibtexParser().Parse(bib);

        Assert.Single(entries);
        Assert.Equal("kept", entries[0].CitationKey);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        var entries = new BibtexParser().Parse("");
        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_MalformedEntry_SilentlySkips()
    {
        var bib = """
            @article{no comma here}
            @article{valid, title = {Valid}}
            """;

        var entries = new BibtexParser().Parse(bib);

        // 第一个条目没有逗号，citation key 后无字段，ParseEntryBody 返回 null
        Assert.Single(entries);
        Assert.Equal("valid", entries[0].CitationKey);
    }

    [Fact]
    public void ParseSingle_ReturnsFirstEntry()
    {
        var bib = """
            @article{single, title = {Only One}}
            """;

        var entry = new BibtexParser().ParseSingle(bib);

        Assert.NotNull(entry);
        Assert.Equal("single", entry.CitationKey);
    }

    [Fact]
    public void ParseSingle_NoEntry_ReturnsNull()
    {
        var entry = new BibtexParser().ParseSingle("no entries here");
        Assert.Null(entry);
    }
}
```

- [ ] **Step 2: 运行测试**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet test tests/WeaveDoc.Converter.Tests --filter "BibtexParserTests" --no-restore -v n`
Expected: 10 tests PASS

- [ ] **Step 3: Commit**

```bash
git add tests/WeaveDoc.Converter.Tests/BibtexParserTests.cs
git commit -m "test(config): add BibtexParser tests covering abbreviation, nesting, and edge cases"
```

---

## Task 7: 最终验证

- [ ] **Step 1: 运行全部测试**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet test tests/WeaveDoc.Converter.Tests -v n`
Expected: 全部通过（AfdStyleMapperTests + PandocPipelineTests + ConfigManagerTests + BibtexParserTests）

- [ ] **Step 2: 验证构建无错误**

Run: `cd "/d/Code All/WorkProgram/WeaveDoc" && dotnet build`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Final commit (if any remaining changes)**

```bash
git add -A
git status
# 如果有未提交的更改，commit 之
```
