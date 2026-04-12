# WeaveDoc.Converter.Ui.Tests

WeaveDoc.Converter.Ui 的 Headless UI 测试项目，在无图形环境下验证 Avalonia 控件的渲染和交互行为。

> **共 6 个测试，全部通过**

---

## 技术栈

| 依赖 | 版本 | 用途 |
| --- | --- | --- |
| xUnit | 2 | 测试框架 |
| Avalonia.Headless | 11.* | 无头 UI 测试基础设施 |
| Avalonia.Headless.XUnit | 11.* | `[AvaloniaFact]` 属性集成 xUnit |
| Avalonia.Themes.Fluent | 11.* | Fluent 主题（测试环境） |
| Avalonia.Fonts.Inter | 11.* | Inter 字体 |
| Avalonia.Controls.DataGrid | 11.* | DataGrid 控件 |
| Microsoft.NET.Test.Sdk | 17 | 测试运行器 |
| .NET | 10 | 目标框架 |

---

## 测试文件

```text
WeaveDoc.Converter.Ui.Tests/
├── TestAppBuilder.cs          # Headless 测试应用配置 + TestApp
├── TemplateTabTests.cs        # 模板管理标签页测试（3 个）
├── ConvertTabTests.cs         # 文档转换向导标签页测试（3 个）
└── WeaveDoc.Converter.Ui.Tests.csproj
```

---

## 测试概览

| 测试类 | 数量 | 类型 | 说明 |
| --- | --- | --- | --- |
| TemplateTabTests | 3 | Headless UI 测试 | DataGrid 数据绑定、种子模板加载、状态栏 |
| ConvertTabTests | 3 | Headless UI 测试 | ComboBox 模板填充、空输入验证、格式默认值 |

### TemplateTabTests（3 个）

| 测试 | 验证内容 |
| --- | --- |
| `TemplateTab_LoadTemplates_DisplaysInGrid` | 保存模板后加载，DataGrid 显示正确的模板数据 |
| `TemplateTab_SeedButton_SeedTemplates` | 执行种子模板后，DataGrid 包含至少 3 个内置模板 |
| `TemplateTab_StatusBar_ShowsTemplateCount` | 空模板库时状态栏显示 "0" |

### ConvertTabTests（3 个）

| 测试 | 验证内容 |
| --- | --- |
| `ConvertTab_LoadTemplates_PopulatesComboBox` | 注入服务后 ComboBox 自动填充 >=3 个模板，且默认选中第一项 |
| `ConvertTab_ConvertWithoutMd_ShowsErrorStatus` | 未选择 Markdown 文件时点击转换，状态标签显示"请选择" |
| `ConvertTab_FormatRadioButtons_DefaultIsDocx` | DOCX 单选按钮默认选中，PDF 未选中 |

---

## Headless 测试机制

### TestAppBuilder

项目通过 `[assembly: AvaloniaTestApplication]` 属性注册 Headless 应用构建器：

```csharp
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .LogToTrace();
}
```

### 测试模式

每个测试使用 `[AvaloniaFact]` 属性（非普通 `[Fact]`），确保测试在 Avalonia UI 线程上执行：

```csharp
[AvaloniaFact]
public async Task MyTest()
{
    var tab = new TemplateTab();
    var window = new Window { Content = tab };
    window.Show();

    tab.SetConfigManager(configManager);

    var grid = tab.FindControl<DataGrid>("TemplateGrid");
    Assert.NotNull(grid);
}
```

### 测试隔离

- 使用 `Path.GetTempPath()` + GUID 创建临时目录
- `IDisposable` 模式确保每个测试结束后清理临时文件
- `ConfigManager` 使用独立的 SQLite 文件，测试间完全隔离

---

## 运行测试

```bash
# 运行全部 UI 测试
dotnet test tests/WeaveDoc.Converter.Ui.Tests -v n

# 运行指定标签页测试
dotnet test tests/WeaveDoc.Converter.Ui.Tests --filter "TemplateTabTests" -v n
dotnet test tests/WeaveDoc.Converter.Ui.Tests --filter "ConvertTabTests" -v n
```
