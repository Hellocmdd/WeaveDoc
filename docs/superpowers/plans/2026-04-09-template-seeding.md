# AFD 模板种子机制实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 ConfigManager 上新增 `EnsureSeedTemplatesAsync()` 方法，将嵌入的 AFD 模板 JSON 自动种子到 SQLite 数据库，使运行时可直接通过 templateId 使用内置模板。

**Architecture:** 将 `TemplateSchemas/*.json` 声明为嵌入资源。`ConfigManager.EnsureSeedTemplatesAsync()` 扫描程序集嵌入资源 → 解析 JSON → 检查数据库是否已存在 → 不存在则调用现有 `SaveTemplateAsync` 写入。冲突策略：跳过已存在的模板。

**Tech Stack:** C# / .NET 10, System.Reflection（嵌入资源）, SQLite（Microsoft.Data.Sqlite）, xUnit

---

## File Structure

| 操作 | 文件路径 | 职责 |
|------|----------|------|
| Modify | `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` | 添加 EmbeddedResource ItemGroup |
| Modify | `src/WeaveDoc.Converter/Config/ConfigManager.cs` | 添加 EnsureSeedTemplatesAsync() 方法 |
| Modify | `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` | 添加 3 个种子测试 |

---

### Task 1: 配置嵌入资源 + 编写第一个测试（空数据库种子全部模板）

**Files:**
- Modify: `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj`
- Modify: `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs`

- [ ] **Step 1: 在 .csproj 中添加嵌入资源配置**

在 `src/WeaveDoc.Converter/WeaveDoc.Converter.csproj` 的 `</Project>` 结束标签前添加：

```xml
  <ItemGroup>
    <EmbeddedResource Include="Config\TemplateSchemas\**\*.json" />
  </ItemGroup>
```

完整的 csproj 文件应为：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DocumentFormat.OpenXml" Version="3.5.1" />
    <PackageReference Include="Markdig" Version="0.39.1" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.5" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Config\TemplateSchemas\**\*.json" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 在 ConfigManagerTests.cs 添加第一个测试**

在 `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` 文件末尾（`DeleteTemplate_RemovesFromDbAndFile` 测试之后、`SaveTemplate_OverwritesExisting` 测试之后），即类的 `}` 之前，添加以下测试方法：

```csharp
    [Fact]
    public async Task EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates()
    {
        await _manager.EnsureSeedTemplatesAsync();

        var courseReport = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(courseReport);
        Assert.Equal("课程报告", courseReport.Meta.TemplateName);

        var labReport = await _manager.GetTemplateAsync("lab-report");
        Assert.NotNull(labReport);
        Assert.Equal("实验报告", labReport.Meta.TemplateName);

        var thesis = await _manager.GetTemplateAsync("default-thesis");
        Assert.NotNull(thesis);
        Assert.Equal("默认学术论文", thesis.Meta.TemplateName);
    }
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates" -v normal`

Expected: FAIL — `ConfigManager` 不包含 `EnsureSeedTemplatesAsync` 的定义。

- [ ] **Step 4: 实现 EnsureSeedTemplatesAsync 方法**

在 `src/WeaveDoc.Converter/Config/ConfigManager.cs` 中：

首先在文件顶部添加 using：

```csharp
using System.Reflection;
```

然后在 `DeleteTemplateAsync` 方法之后（类的 `}` 之前），添加以下方法：

```csharp
    /// <summary>
    /// 将嵌入资源中的内置 AFD 模板种子到数据库。已存在的模板不会被覆盖。
    /// </summary>
    public async Task EnsureSeedTemplatesAsync()
    {
        const string prefix = "WeaveDoc.Converter.Config.TemplateSchemas.";
        const string suffix = ".json";

        var assembly = typeof(ConfigManager).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix) && name.EndsWith(suffix));

        foreach (var resourceName in resourceNames)
        {
            var templateId = resourceName[prefix.Length..^suffix.Length];

            var existingPath = await _repository.GetJsonPathAsync(templateId);
            if (existingPath != null)
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var template = _parser.ParseJson(json);
            await SaveTemplateAsync(templateId, template);
        }
    }
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "EnsureSeedTemplatesAsync_WithEmptyDb_SeedsAllTemplates" -v normal`

Expected: PASS。

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter/WeaveDoc.Converter.csproj src/WeaveDoc.Converter/Config/ConfigManager.cs tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs
git commit -m "feat(converter): add EnsureSeedTemplatesAsync for built-in template discovery"
```

---

### Task 2: 测试跳过已存在的模板

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs`

- [ ] **Step 1: 添加跳过测试**

在 `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` 中（上一个测试之后），添加：

```csharp
    [Fact]
    public async Task EnsureSeedTemplatesAsync_SkipsExistingTemplates()
    {
        // 先保存一个修改版的 course-report（Description 不同）
        var modified = CreateTestTemplate("课程报告");
        modified = modified with
        {
            Meta = modified.Meta with { Description = "用户自定义版" }
        };
        await _manager.SaveTemplateAsync("course-report", modified);

        await _manager.EnsureSeedTemplatesAsync();

        var result = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(result);
        // 验证是用户自定义版，不是内置版（内置版 Description = "高校课程报告通用模板"）
        Assert.Equal("用户自定义版", result.Meta.Description);
    }
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "EnsureSeedTemplatesAsync_SkipsExistingTemplates" -v normal`

Expected: PASS。

- [ ] **Step 3: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs
git commit -m "test(converter): add skip-existing test for EnsureSeedTemplatesAsync"
```

---

### Task 3: 测试幂等性

**Files:**
- Modify: `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs`

- [ ] **Step 1: 添加幂等性测试**

在 `tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs` 中（上一个测试之后），添加：

```csharp
    [Fact]
    public async Task EnsureSeedTemplatesAsync_Idempotent()
    {
        await _manager.EnsureSeedTemplatesAsync();
        await _manager.EnsureSeedTemplatesAsync();

        var result = await _manager.GetTemplateAsync("course-report");
        Assert.NotNull(result);
        Assert.Equal("课程报告", result.Meta.TemplateName);
    }
```

- [ ] **Step 2: 运行全部种子测试**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj --filter "EnsureSeedTemplatesAsync" -v normal`

Expected: 3 个测试全部 PASS。

- [ ] **Step 3: 运行全量测试确认无回归**

Run: `dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -v normal`

Expected: 全部 PASS。

- [ ] **Step 4: 提交**

```bash
git add tests/WeaveDoc.Converter.Tests/ConfigManagerTests.cs
git commit -m "test(converter): add idempotent test for EnsureSeedTemplatesAsync"
```
