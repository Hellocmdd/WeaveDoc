# 本地配置管理（Task 3.3）设计规格

> 版本：v1.0 | 日期：2026-04-07 | 负责人：任逸青

## 1. 概述

实现 WeaveDoc 本地配置管理模块，提供模板库 CRUD 和 BibTeX 文献解析能力。本模块是 `DocumentConversionEngine` 的依赖项，通过 `ConfigManager` 对外暴露统一 API。

### 验收标准（F-04）

能导入/管理 BibTeX 文献，并切换样式模板。

## 2. 架构决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| SQLite 库 | Microsoft.Data.Sqlite | 微软官方维护，与 .NET 生态最契合，原生 ADO.NET API |
| 存储策略 | SQLite 存元信息 + JSON 文件存模板内容 | 与现有 stub 签名一致，JSON 文件可手动编辑、方便版本控制 |
| BibTeX 解析深度 | 实用级 | 覆盖 95% 学术场景，支持字符串缩写和嵌套大括号 |
| 版本快照 | 本次不实现 | 核心功能优先，未来扩展时加 version 列即可 |
| ORM | 不引入 | 单表结构，原生 SQL 足够，避免额外依赖 |

## 3. 组件设计

### 3.1 TemplateRepository（internal，SQLite 元信息层）

**职责**：SQLite 数据库创建、模板元信息 CRUD、JSON 文件路径映射。

**数据库表结构**：

```sql
CREATE TABLE IF NOT EXISTS templates (
    template_id  TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    version      TEXT NOT NULL,
    author       TEXT NOT NULL,
    description  TEXT NOT NULL,
    json_path    TEXT NOT NULL,
    created_at   TEXT NOT NULL,
    updated_at   TEXT NOT NULL
);
```

**方法签名与实现**：

| 方法 | SQL / 实现 |
|------|-----------|
| `InitializeAsync()` | 创建目录 + `CREATE TABLE IF NOT EXISTS` |
| `GetMetaAsync(id)` | `SELECT * FROM templates WHERE template_id = @id` → `AfdMeta?` |
| `GetAllMetasAsync()` | `SELECT * FROM templates ORDER BY name` → `List<AfdMeta>` |
| `UpsertAsync(id, meta, jsonPath)` | `INSERT OR REPLACE INTO templates ...` |
| `DeleteAsync(id)` | `DELETE FROM templates WHERE template_id = @id` |

**初始化策略**：`InitializeAsync()` 使用懒初始化——每个公开方法内部检查 `_initialized` 标志，未初始化则自动调用。无需显式初始化调用。

**连接策略**：每次操作开闭连接（SQLite 文件级锁，无需连接池）。

### 3.2 ConfigManager（public，对外 API 层）

**职责**：编排 TemplateRepository + AfdParser + 文件系统操作的统一入口。

**依赖关系**：

```
ConfigManager
├── TemplateRepository (internal)
├── AfdParser (复用已有)
└── 文件系统操作
```

**构造函数**：

```csharp
public ConfigManager(string dbPath)
{
    _repository = new TemplateRepository(dbPath);
    _parser = new AfdParser();
    _templatesDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "templates");
}
```

**方法流程**：

| 方法 | 流程 |
|------|------|
| `GetTemplateAsync(id)` | 1. `_repository.GetMetaAsync(id)` 获取 jsonPath<br>2. `_parser.Parse(jsonPath)` 解析完整模板<br>3. 返回 `AfdTemplate?` |
| `ListTemplatesAsync()` | 委托 `_repository.GetAllMetasAsync()` |
| `SaveTemplateAsync(id, template)` | 1. 确保 `_templatesDir` 存在<br>2. `JsonSerializer.Serialize` 写入 `_templatesDir/{id}.json`<br>3. `_repository.UpsertAsync(id, template.Meta, jsonPath)` |
| `DeleteTemplateAsync(id)` | 1. 获取 meta 找到 jsonPath<br>2. 删除 JSON 文件<br>3. `_repository.DeleteAsync(id)` |

**序列化**：使用 `System.Text.Json`，camelCase 命名策略（与现有 JSON 模板一致）。

### 3.3 BibtexParser（public，BibTeX 解析器）

**职责**：将 `.bib` 文件内容解析为结构化的 `BibtexEntry` 列表。纯文本解析，无外部依赖。

**解析流程**：

```
Parse(bibContent)
  ├─ 第一遍：扫描 @String 定义 → 构建缩写映射表
  ├─ 第二遍：逐条目解析
  │   ├─ 括号计数器定位条目边界
  │   ├─ 提取 entry type + citation key
  │   ├─ 逐字段解析 field = value
  │   │   ├─ {value}: 括号计数提取
  │   │   ├─ "value": 引号配对提取
  │   │   └─ bare_word: 查缩写映射表替换
  │   └─ 构造 BibtexEntry
  └─ 返回 List<BibtexEntry>
```

**特性**：

- 支持 `@String{key = "value"}` 字符串缩写定义和展开
- 括号计数器处理嵌套大括号（如 `title = {A {Nested} Title}`）
- 支持三种值格式：`{}`、`""`、裸字（bare word）
- 跳过 `@Comment`、`@Preamble`
- 格式错误的条目静默跳过，不抛异常
- `ParseSingle(entryText)` 复用同一解析逻辑，处理单个条目

## 4. 数据流

```
DocumentConversionEngine
  │
  └─ ConfigManager.GetTemplateAsync("default-thesis")
       │
       ├─ TemplateRepository.GetMetaAsync("default-thesis")
       │   └─ SQLite → AfdMeta + jsonPath
       │
       └─ AfdParser.Parse(jsonPath)
           └─ JSON 文件 → AfdTemplate
```

## 5. 测试策略

| 测试类 | 覆盖内容 |
|--------|----------|
| `ConfigManagerTests` | CRUD 全流程：Save → Get → List → Delete；使用临时目录和临时 SQLite |
| `BibtexParserTests` | 基本条目、嵌套大括号、字符串缩写、多值字段、空输入、格式错误、ParseSingle |

ConfigManager 测试使用真实文件系统和真实 SQLite（临时目录），不做 mock。

## 6. NuGet 依赖变更

| 项目 | 新增包 |
|------|--------|
| `WeaveDoc.Converter.csproj` | `Microsoft.Data.Sqlite` (latest stable) |

测试项目无需新增包（已有 xUnit + DocumentFormat.OpenXml）。

## 7. 文件变更清单

| 文件 | 操作 |
|------|------|
| `src/WeaveDoc.Converter/Config/TemplateRepository.cs` | 实现（stub → 完整代码） |
| `src/WeaveDoc.Converter/Config/ConfigManager.cs` | 实现（stub → 完整代码） |
| `src/WeaveDoc.Converter/Config/BibtexParser.cs` | 实现（stub → 完整代码） |
| `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` | 添加 Microsoft.Data.Sqlite |
| `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` | 编写 ConfigManager + BibtexParser 测试 |

## 8. 未来扩展（本次不实现）

- 版本增量快照：在 `templates` 表加 `version` 列 + JSON 文件版本目录
- BibTeX 文献库管理：将解析后的 BibTeX 条目存入 SQLite，支持检索和引用插入
- 模板导入/导出：从远程或 ZIP 文件导入模板
