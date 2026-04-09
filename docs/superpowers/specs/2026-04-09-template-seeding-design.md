# AFD 模板种子机制设计

**Goal:** 让 `TemplateSchemas/` 目录下的 AFD 模板 JSON 文件在运行时自动可发现并导入到 ConfigManager 的 SQLite 存储中，使 `DocumentConversionEngine` 能直接通过 templateId 使用内置模板。

**Date:** 2026-04-09

---

## 问题

当前 `TemplateSchemas/` 下的 3 个 JSON 模板（`default-thesis.json`、`course-report.json`、`lab-report.json`）是"死文件"：
- 未在 `.csproj` 中声明为嵌入资源或 Content，运行时无法访问
- ConfigManager 只有显式的 `SaveTemplateAsync`/`GetTemplateAsync`，无批量初始化
- DocumentConversionEngine 在数据库中找不到模板时直接返回失败

## 设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 冲突策略 | 跳过已存在的模板 | 保护用户可能的自定义修改 |
| 资源访问方式 | 嵌入资源（Embedded Resource） | 零文件依赖，部署简单 |
| 触发时机 | 显式调用 `EnsureSeedTemplatesAsync()` | 可控、可测试、不隐藏副作用 |
| 实现位置 | ConfigManager 内置方法 | 改动最小，复用已有 _parser/_repository/SaveTemplateAsync |

## 方案（已选定：方案 A）

在 ConfigManager 上新增 `EnsureSeedTemplatesAsync()` 公共方法。

### 1. 嵌入资源配置

在 `WeaveDoc.Converter.csproj` 中添加：

```xml
<ItemGroup>
  <EmbeddedResource Include="Config\TemplateSchemas\**\*.json" />
</ItemGroup>
```

编译后，JSON 文件可通过 `Assembly.GetManifestResourceNames()` 获取，资源名格式为 `WeaveDoc.Converter.Config.TemplateSchemas.{filename}.json`。

### 2. EnsureSeedTemplatesAsync() 方法

**签名**：`public async Task EnsureSeedTemplatesAsync()`

**逻辑流程**：

1. 获取程序集中所有以 `WeaveDoc.Converter.Config.TemplateSchemas.` 开头、以 `.json` 结尾的嵌入资源名
2. 对每个资源：
   - 提取 `templateId`：去掉前缀 `WeaveDoc.Converter.Config.TemplateSchemas.` 和后缀 `.json`
   - 读取嵌入资源流为 JSON 字符串
   - 用 `_parser.ParseJson(json)` 解析为 `AfdTemplate`
   - 用 `_repository.GetJsonPathAsync(templateId)` 检查数据库是否已有该 templateId
   - 如果已存在（返回非 null），跳过
   - 如果不存在，调用现有 `SaveTemplateAsync(templateId, template)` 写入数据库和文件系统

**为什么不直接检查文件系统**：ConfigManager 的 `_templatesDir` 由数据库驱动。`GetJsonPathAsync` 返回 null 意味着数据库无记录，即使文件意外存在也不会被使用。所以检查数据库是正确的判重方式。

### 3. 修改的文件

| 操作 | 文件路径 | 改动 |
|------|----------|------|
| Modify | `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` | 添加 EmbeddedResource ItemGroup |
| Modify | `src/WeaveDoc.Converter/Config/ConfigManager.cs` | 添加 EnsureSeedTemplatesAsync() 方法 |

不新增任何类或文件。

### 4. 测试策略

三个测试方法，添加到现有 `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs`（该文件已有 Save/Get/List/Delete 测试，使用 IDisposable + 临时目录模式）：

**Test 1: `EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates`**

- 使用构造函数初始化的 `_manager`（空数据库）
- 调用 `await _manager.EnsureSeedTemplatesAsync()`
- 验证 `GetTemplateAsync("course-report")` 返回的 `template.Meta.TemplateName == "课程报告"`
- 验证 `GetTemplateAsync("lab-report")` 返回 `"实验报告"`
- 验证 `GetTemplateAsync("default-thesis")` 返回 `"默认学术论文"`

**Test 2: `EnsureSeedTemplatesAsync_SkipsExistingTemplates`**

- 使用 `_manager`，先 `SaveTemplateAsync("course-report", modifiedTemplate)` 保存一个 Description 为 "用户自定义版" 的修改版
- 调用 `await _manager.EnsureSeedTemplatesAsync()`
- 验证 `GetTemplateAsync("course-report")` 的 `Description == "用户自定义版"`（非内置的 "高校课程报告通用模板"）

**Test 3: `EnsureSeedTemplatesAsync_Idempotent`**

- 使用 `_manager`（空数据库）
- 调用两次 `await _manager.EnsureSeedTemplatesAsync()`
- 第二次不抛异常
- `GetTemplateAsync("course-report")` 返回一致结果

### 5. 调用方使用示例

```csharp
var configManager = new ConfigManager(dbPath);
await configManager.EnsureSeedTemplatesAsync();  // 应用启动时调用一次

// 之后正常使用
var engine = new DocumentConversionEngine(pipeline, configManager);
var result = await engine.ConvertAsync(mdPath, "course-report", "docx");
```

## 不做的事情

- **不在 ConfigManager 构造函数中自动 seed**：保持显式调用，避免隐藏副作用
- **不做版本感知更新**：YAGNI，跳过策略已足够
- **不做混合模式（文件系统 + 嵌入资源）**：嵌入资源已满足需求，混合模式增加复杂度
- **不修改 DocumentConversionEngine**：种子写入后，现有引擎逻辑无需任何改动
