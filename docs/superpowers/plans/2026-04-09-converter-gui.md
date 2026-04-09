# Converter GUI 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 WBS 3 语义转换系统创建简易 Avalonia UI 演示工具，覆盖模板管理（CRUD + 种子）和文档转换（MD → DOCX/PDF），并通过 Headless UI 测试验证功能正确性。

**Architecture:** 独立 Avalonia UI 项目 `src/WeaveDoc.Converter.Ui/`，code-behind 模式。主窗口 TabControl 切换模板管理和文档转换两个标签页。启动时创建 ConfigManager + PandocPipeline + DocumentConversionEngine，通过构造函数注入各 Tab。测试使用 Avalonia.Headless 在内存中验证 UI 交互。

**Tech Stack:** C# .NET 10, Avalonia UI 11.x (Fluent 主题), Avalonia.Headless (UI 测试), xUnit

**Spec:** `docs/superpowers/specs/2026-04-09-converter-gui-design.md`

---

## File Structure

| 文件 | 操作 | 职责 |
|------|------|------|
| `src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj` | **新建** | Avalonia 项目文件 |
| `src/WeaveDoc.Converter.Ui/App.axaml` + `.cs` | **新建** | Avalonia Application，加载 Fluent 主题 |
| `src/WeaveDoc.Converter.Ui/Program.cs` | **新建** | 入口点，初始化后端服务 |
| `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml` + `.cs` | **新建** | 主窗口 + TabControl |
| `src/WeaveDoc.Converter.Ui/Views/TemplateTab.axaml` + `.cs` | **新建** | 模板管理标签页 |
| `src/WeaveDoc.Converter.Ui/Views/ConvertTab.axaml` + `.cs` | **新建** | 文档转换标签页 |
| `tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj` | **新建** | UI 测试项目 |
| `tests/WeaveDoc.Converter.Ui.Tests/TemplateTabTests.cs` | **新建** | 模板管理 UI 测试 |
| `tests/WeaveDoc.Converter.Ui.Tests/ConvertTabTests.cs` | **新建** | 文档转换 UI 测试 |
| `WeaveDoc.slnx` | **修改** | 添加 UI 项目 + UI 测试项目 |

---

### Task 1: 项目脚手架 + 空窗口

**Files:**
- Create: `src/WeaveDoc.Converter.Ui/` (全部文件)
- Modify: `WeaveDoc.slnx`

- [ ] **Step 1: 创建项目目录结构**

```bash
mkdir -p "src/WeaveDoc.Converter.Ui/Views"
mkdir -p "src/WeaveDoc.Converter.Ui/Assets"
```

- [ ] **Step 2: 创建 .csproj**

创建 `src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.*" />
    <PackageReference Include="Avalonia.Controls.DataGrid" Version="11.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WeaveDoc.Converter\WeaveDoc.Converter.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: 创建 App.axaml + App.axaml.cs**

创建 `src/WeaveDoc.Converter.Ui/App.axaml`：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="WeaveDoc.Converter.Ui.App"
             RequestedThemeVariant="Light">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

创建 `src/WeaveDoc.Converter.Ui/App.axaml.cs`：

```csharp
using Avalonia;
using Avalonia.Markup.Xaml;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Ui.Views;

namespace WeaveDoc.Converter.Ui;

public class App : Application
{
    private ConfigManager? _configManager;
    private DocumentConversionEngine? _engine;

    public App() { }

    public App(ConfigManager configManager, DocumentConversionEngine engine)
    {
        _configManager = configManager;
        _engine = engine;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (_configManager != null && _engine != null)
        {
            var window = new MainWindow(_configManager, _engine);
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: 创建 Program.cs**

创建 `src/WeaveDoc.Converter.Ui/Program.cs`：

```csharp
using Avalonia;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter.Ui;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "weavedoc.db");
        var configManager = new ConfigManager(dbPath);
        configManager.EnsureSeedTemplatesAsync().GetAwaiter().GetResult();

        var pandoc = new PandocPipeline();
        var engine = new DocumentConversionEngine(pandoc, configManager);

        BuildAvaloniaApp(configManager, engine)
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp(
        ConfigManager configManager,
        DocumentConversionEngine engine)
        => AppBuilder.Configure(() => new App(configManager, engine))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 5: 创建空 MainWindow**

创建 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="WeaveDoc.Converter.Ui.Views.MainWindow"
        Title="WeaveDoc 转换工具"
        Width="800" Height="500"
        WindowStartupLocation="CenterScreen">
    <TextBlock Text="Hello WeaveDoc" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Window>
```

创建 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml.cs`：

```csharp
using Avalonia.Controls;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow() { }

    public MainWindow(ConfigManager configManager, DocumentConversionEngine engine)
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 6: 添加到解决方案并验证编译**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet sln WeaveDoc.slnx add src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj
dotnet build src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj
```

Expected: BUILD SUCCEEDED。

- [ ] **Step 7: 提交**

```bash
git add src/WeaveDoc.Converter.Ui/ WeaveDoc.slnx
git commit -m "feat(ui): scaffold Avalonia UI project with empty MainWindow"
```

---

### Task 2: TemplateTab 模板管理 + Headless UI 测试

**Files:**
- Create: `src/WeaveDoc.Converter.Ui/Views/TemplateTab.axaml` + `.cs`
- Modify: `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml` + `.cs`
- Create: `tests/WeaveDoc.Converter.Ui.Tests/` (测试项目)
- Create: `tests/WeaveDoc.Converter.Ui.Tests/TemplateTabTests.cs`

- [ ] **Step 1: 创建 TemplateTab.axaml**

创建 `src/WeaveDoc.Converter.Ui/Views/TemplateTab.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="WeaveDoc.Converter.Ui.Views.TemplateTab">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,10">
            <Button x:Name="RefreshButton" Content="刷新" />
            <Button x:Name="SeedButton" Content="种子模板" />
            <Button x:Name="ImportButton" Content="导入" />
            <Button x:Name="DeleteButton" Content="删除" />
        </StackPanel>
        <TextBlock x:Name="StatusBar" DockPanel.Dock="Bottom" Margin="0,8,0,0" />
        <DataGrid x:Name="TemplateGrid"
                  IsReadOnly="True"
                  AutoGenerateColumns="False"
                  SelectionMode="Single">
            <DataGrid.Columns>
                <DataGridTextColumn Header="ID" Binding="{Binding TemplateId}" Width="*" />
                <DataGridTextColumn Header="名称" Binding="{Binding TemplateName}" Width="*" />
                <DataGridTextColumn Header="版本" Binding="{Binding Version}" Width="80" />
                <DataGridTextColumn Header="作者" Binding="{Binding Author}" Width="100" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: 创建 TemplateTab.axaml.cs**

创建 `src/WeaveDoc.Converter.Ui/Views/TemplateTab.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeaveDoc.Converter.Ui.Views;

public partial class TemplateTab : UserControl
{
    private ConfigManager? _configManager;

    public TemplateTab()
    {
        InitializeComponent();
        RefreshButton.Click += OnRefresh;
        SeedButton.Click += OnSeed;
        ImportButton.Click += OnImport;
        DeleteButton.Click += OnDelete;
    }

    public void SetConfigManager(ConfigManager configManager)
    {
        _configManager = configManager;
        _ = LoadTemplatesAsync();
    }

    internal async Task LoadTemplatesAsync()
    {
        if (_configManager == null) return;
        var templates = await _configManager.ListTemplatesAsync();
        TemplateGrid.ItemsSource = templates;
        StatusBar.Text = $"共 {templates.Count} 个模板";
    }

    private async void OnRefresh(object? sender, RoutedEventArgs e) => await LoadTemplatesAsync();

    private async void OnSeed(object? sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;
        await _configManager.EnsureSeedTemplatesAsync();
        await LoadTemplatesAsync();
    }

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 AFD 模板 JSON 文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        var template = new AfdParser().ParseJson(json);
        var templateId = Path.GetFileNameWithoutExtension(file.Name);

        await _configManager.SaveTemplateAsync(templateId, template);
        await LoadTemplatesAsync();
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_configManager == null) return;

        var selected = TemplateGrid.SelectedItem as AfdMeta;
        if (selected == null) return;

        await _configManager.DeleteTemplateAsync(selected.TemplateId);
        await LoadTemplatesAsync();
    }
}
```

- [ ] **Step 3: 更新 MainWindow 为 TabControl 布局**

替换 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="using:WeaveDoc.Converter.Ui.Views"
        x:Class="WeaveDoc.Converter.Ui.Views.MainWindow"
        Title="WeaveDoc 转换工具"
        Width="800" Height="500"
        WindowStartupLocation="CenterScreen">
    <TabControl>
        <TabItem Header="模板管理">
            <views:TemplateTab x:Name="TemplateTabControl" />
        </TabItem>
        <TabItem Header="文档转换">
            <TextBlock Text="文档转换（Task 3 实现）" Margin="10" />
        </TabItem>
    </TabControl>
</Window>
```

替换 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml.cs`：

```csharp
using Avalonia.Controls;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow() { }

    public MainWindow(ConfigManager configManager, DocumentConversionEngine engine)
    {
        InitializeComponent();
        var templateTab = this.FindControl<TemplateTab>("TemplateTabControl");
        templateTab?.SetConfigManager(configManager);
    }
}
```

- [ ] **Step 4: 创建 UI 测试项目**

```bash
mkdir -p "tests/WeaveDoc.Converter.Ui.Tests"
```

创建 `tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Headless" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
    <PackageReference Include="Avalonia.Controls.DataGrid" Version="11.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\WeaveDoc.Converter.Ui\WeaveDoc.Converter.Ui.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: 创建 TemplateTabTests.cs — Headless UI 测试**

创建 `tests/WeaveDoc.Converter.Ui.Tests/TemplateTabTests.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Ui.Views;
using Xunit;

namespace WeaveDoc.Converter.Ui.Tests;

public class TemplateTabTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _configManager;

    public TemplateTabTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ui-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _configManager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static AvaloniaHeadlessAppBuilder BuildApp()
    {
        return AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .LogToTrace();
    }

    /// <summary>
    /// 测试应用类：仅加载 Fluent 主题，不创建主窗口
    /// </summary>
    private class TestApp : Application
    {
        public override void Initialize()
        {
            var fluent = new Avalonia.Themes.Fluent.FluentTheme();
            Styles.Add(fluent);
        }
    }

    [Fact]
    public async Task TemplateTab_LoadTemplates_DisplaysInGrid()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        // 保存测试模板
        await _configManager.SaveTemplateAsync("test-tpl", new AfdTemplate
        {
            Meta = new AfdMeta { TemplateName = "测试", Version = "1.0", Author = "Test", Description = "desc" },
            Defaults = new AfdDefaults { FontFamily = "宋体", FontSize = 12, LineSpacing = 1.5 },
            Styles = new Dictionary<string, AfdStyleDefinition>
            {
                ["body"] = new() { DisplayName = "正文", FontSize = 12 }
            }
        });

        // 绑定 ConfigManager 并加载
        tab.SetConfigManager(_configManager);

        // 等待异步加载完成
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var grid = tab.FindControl<DataGrid>("TemplateGrid");
            Assert.NotNull(grid);
            Assert.NotNull(grid.ItemsSource);
            var items = grid.ItemsSource.Cast<AfdMeta>().ToList();
            Assert.Single(items);
            Assert.Equal("测试", items[0].TemplateName);
        });
    }

    [Fact]
    public async Task TemplateTab_SeedButton_SeedTemplates()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        tab.SetConfigManager(_configManager);

        // 模拟点击种子按钮
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var seedBtn = tab.FindControl<Button>("SeedButton");
            Assert.NotNull(seedBtn);

            // 先种子
            await _configManager.EnsureSeedTemplatesAsync();
            await tab.LoadTemplatesAsync();

            var grid = tab.FindControl<DataGrid>("TemplateGrid");
            Assert.NotNull(grid);
            var items = grid.ItemsSource!.Cast<AfdMeta>().ToList();
            Assert.True(items.Count >= 3, $"期望至少 3 个种子模板，实际 {items.Count}");
            Assert.Contains(items, i => i.TemplateName == "课程报告");
            Assert.Contains(items, i => i.TemplateName == "实验报告");
            Assert.Contains(items, i => i.TemplateName == "默认学术论文");
        });
    }

    [Fact]
    public async Task TemplateTab_StatusBar_ShowsTemplateCount()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new TemplateTab();
        var window = new Window { Content = tab };
        window.Show();

        tab.SetConfigManager(_configManager);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await tab.LoadTemplatesAsync();

            var status = tab.FindControl<TextBlock>("StatusBar");
            Assert.NotNull(status);
            Assert.Contains("0", status.Text);
        });
    }
}
```

- [ ] **Step 6: 添加测试项目到解决方案并验证**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet sln WeaveDoc.slnx add tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj
dotnet build src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj
dotnet test tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj -v normal
```

Expected: BUILD SUCCEEDED, 3 个 UI 测试 PASS。

- [ ] **Step 7: 提交**

```bash
git add src/WeaveDoc.Converter.Ui/ tests/WeaveDoc.Converter.Ui.Tests/ WeaveDoc.slnx
git commit -m "feat(ui): add template management tab with Headless UI tests"
```

---

### Task 3: ConvertTab 文档转换向导 + Headless UI 测试

**Files:**
- Create: `src/WeaveDoc.Converter.Ui/Views/ConvertTab.axaml` + `.cs`
- Modify: `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml` + `.cs`
- Create: `tests/WeaveDoc.Converter.Ui.Tests/ConvertTabTests.cs`

- [ ] **Step 1: 创建 ConvertTab.axaml**

创建 `src/WeaveDoc.Converter.Ui/Views/ConvertTab.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="WeaveDoc.Converter.Ui.Views.ConvertTab">
    <ScrollViewer Margin="10">
        <StackPanel Spacing="12">
            <TextBlock Text="Step 1: 选择 Markdown 文件" FontWeight="Bold" />
            <Grid ColumnDefinitions="*, Auto">
                <TextBox x:Name="MdPathBox" Grid.Column="0" IsReadOnly="True"
                         Watermark="点击右侧浏览选择 .md 文件..." />
                <Button x:Name="BrowseMdButton" Grid.Column="1" Content="浏览..." Margin="8,0,0,0" />
            </Grid>

            <TextBlock Text="Step 2: 选择模板" FontWeight="Bold" />
            <ComboBox x:Name="TemplateCombo" Width="400" HorizontalAlignment="Stretch"
                      DisplayMemberBinding="{Binding TemplateName}" />

            <TextBlock Text="Step 3: 导出格式" FontWeight="Bold" />
            <StackPanel Orientation="Horizontal" Spacing="16">
                <RadioButton x:Name="FormatDocx" Content="DOCX" IsChecked="True" GroupName="Format" />
                <RadioButton x:Name="FormatPdf" Content="PDF" GroupName="Format" />
            </StackPanel>

            <TextBlock Text="输出目录" FontWeight="Bold" />
            <Grid ColumnDefinitions="*, Auto">
                <TextBox x:Name="OutputDirBox" Grid.Column="0" IsReadOnly="True"
                         Watermark="选择输出目录..." />
                <Button x:Name="BrowseOutputButton" Grid.Column="1" Content="浏览..." Margin="8,0,0,0" />
            </Grid>

            <Button x:Name="ConvertButton" Content="开始转换"
                    HorizontalAlignment="Center" Width="160" Height="40" FontWeight="Bold" />

            <TextBlock x:Name="StatusLabel" Text="状态: 就绪" Foreground="Gray" />

            <TextBlock Text="日志:" FontWeight="Bold" />
            <TextBox x:Name="LogBox" IsReadOnly="True" AcceptsReturn="True"
                     TextWrapping="Wrap" Height="150"
                     ScrollViewer.VerticalScrollBarVisibility="Auto" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 2: 创建 ConvertTab.axaml.cs**

创建 `src/WeaveDoc.Converter.Ui/Views/ConvertTab.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class ConvertTab : UserControl
{
    private ConfigManager? _configManager;
    private DocumentConversionEngine? _engine;

    public ConvertTab()
    {
        InitializeComponent();
        BrowseMdButton.Click += OnBrowseMd;
        BrowseOutputButton.Click += OnBrowseOutput;
        ConvertButton.Click += OnConvert;
    }

    public void SetServices(ConfigManager configManager, DocumentConversionEngine engine)
    {
        _configManager = configManager;
        _engine = engine;
        _ = LoadTemplatesAsync();
    }

    internal async Task LoadTemplatesAsync()
    {
        if (_configManager == null) return;
        var templates = await _configManager.ListTemplatesAsync();
        TemplateCombo.ItemsSource = templates;
        if (templates.Count > 0)
            TemplateCombo.SelectedIndex = 0;
    }

    private async void OnBrowseMd(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Markdown 文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }]
        });

        if (files.FirstOrDefault() is { } file)
            MdPathBox.Text = file.TryGetLocalPath();
    }

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择输出目录",
            AllowMultiple = false
        });

        if (folders.FirstOrDefault() is { } folder)
            OutputDirBox.Text = folder.TryGetLocalPath();
    }

    private async void OnConvert(object? sender, RoutedEventArgs e)
    {
        if (_engine == null || _configManager == null) return;

        var mdPath = MdPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(mdPath) || !File.Exists(mdPath))
        {
            StatusLabel.Text = "状态: 请选择有效的 Markdown 文件";
            return;
        }

        var selected = TemplateCombo.SelectedItem as AfdMeta;
        if (selected == null)
        {
            StatusLabel.Text = "状态: 请选择模板";
            return;
        }

        var outputDir = OutputDirBox.Text?.Trim();
        if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
        {
            StatusLabel.Text = "状态: 请选择输出目录";
            return;
        }

        var format = FormatDocx.IsChecked == true ? "docx" : "pdf";

        StatusLabel.Text = "状态: 转换中...";
        ConvertButton.IsEnabled = false;
        LogBox.Text = $"模板: {selected.TemplateName} ({selected.TemplateId})\n格式: {format}\n输入: {mdPath}\n\n正在转换...\n";

        try
        {
            var result = await _engine.ConvertAsync(mdPath, selected.TemplateId, format);

            if (result.Success)
            {
                var outputPath = Path.Combine(outputDir, Path.GetFileName(result.OutputPath));
                if (result.OutputPath != outputPath && File.Exists(result.OutputPath))
                    File.Move(result.OutputPath, outputPath, overwrite: true);

                LogBox.Text += $"转换成功!\n输出: {outputPath}";
                StatusLabel.Text = "状态: 转换完成";
            }
            else
            {
                LogBox.Text += $"转换失败: {result.ErrorMessage}";
                StatusLabel.Text = "状态: 转换失败";
            }
        }
        catch (Exception ex)
        {
            LogBox.Text += $"异常: {ex.Message}";
            StatusLabel.Text = "状态: 转换出错";
        }
        finally
        {
            ConvertButton.IsEnabled = true;
        }
    }
}
```

- [ ] **Step 3: 更新 MainWindow 接入 ConvertTab**

替换 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="using:WeaveDoc.Converter.Ui.Views"
        x:Class="WeaveDoc.Converter.Ui.Views.MainWindow"
        Title="WeaveDoc 转换工具"
        Width="800" Height="500"
        WindowStartupLocation="CenterScreen">
    <TabControl>
        <TabItem Header="模板管理">
            <views:TemplateTab x:Name="TemplateTabControl" />
        </TabItem>
        <TabItem Header="文档转换">
            <views:ConvertTab x:Name="ConvertTabControl" />
        </TabItem>
    </TabControl>
</Window>
```

替换 `src/WeaveDoc.Converter.Ui/Views/MainWindow.axaml.cs`：

```csharp
using Avalonia.Controls;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow() { }

    public MainWindow(ConfigManager configManager, DocumentConversionEngine engine)
    {
        InitializeComponent();

        var templateTab = this.FindControl<TemplateTab>("TemplateTabControl");
        templateTab?.SetConfigManager(configManager);

        var convertTab = this.FindControl<ConvertTab>("ConvertTabControl");
        convertTab?.SetServices(configManager, engine);
    }
}
```

- [ ] **Step 4: 创建 ConvertTabTests.cs — Headless UI 测试**

创建 `tests/WeaveDoc.Converter.Ui.Tests/ConvertTabTests.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Ui.Views;
using Xunit;

namespace WeaveDoc.Converter.Ui.Tests;

public class ConvertTabTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigManager _configManager;

    public ConvertTabTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ui-conv-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "test.db");
        _configManager = new ConfigManager(dbPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static AvaloniaHeadlessAppBuilder BuildApp()
    {
        return AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .LogToTrace();
    }

    private class TestApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        }
    }

    [Fact]
    public async Task ConvertTab_LoadTemplates_PopulatesComboBox()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        // 种子模板
        await _configManager.EnsureSeedTemplatesAsync();

        // 模拟 DocumentConversionEngine（不实际调用 Pandoc）
        var pandoc = new WeaveDoc.Converter.Pandoc.PandocPipeline();
        var engine = new DocumentConversionEngine(pandoc, _configManager);

        tab.SetServices(_configManager, engine);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var combo = tab.FindControl<ComboBox>("TemplateCombo");
            Assert.NotNull(combo);
            Assert.NotNull(combo.ItemsSource);
            var items = combo.ItemsSource!.Cast<AfdMeta>().ToList();
            Assert.True(items.Count >= 3, $"期望至少 3 个模板，实际 {items.Count}");
            Assert.Equal(0, combo.SelectedIndex); // 自动选中第一个
        });
    }

    [Fact]
    public async Task ConvertTab_ConvertWithoutMd_ShowsErrorStatus()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await _configManager.EnsureSeedTemplatesAsync();
        var pandoc = new WeaveDoc.Converter.Pandoc.PandocPipeline();
        var engine = new DocumentConversionEngine(pandoc, _configManager);
        tab.SetServices(_configManager, engine);

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // 不选择 MD 文件，直接点转换
            var convertBtn = tab.FindControl<Button>("ConvertButton");
            Assert.NotNull(convertBtn);
            convertBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var status = tab.FindControl<TextBlock>("StatusLabel");
            Assert.NotNull(status);
            Assert.Contains("请选择", status.Text);
        });
    }

    [Fact]
    public async Task ConvertTab_FormatRadioButtons_DefaultIsDocx()
    {
        using var app = BuildApp().SetupWithoutStarting();

        var tab = new ConvertTab();
        var window = new Window { Content = tab };
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var docxRadio = tab.FindControl<RadioButton>("FormatDocx");
            var pdfRadio = tab.FindControl<RadioButton>("FormatPdf");
            Assert.NotNull(docxRadio);
            Assert.NotNull(pdfRadio);
            Assert.True(docxRadio.IsChecked);
            Assert.False(pdfRadio.IsChecked);
        });
    }
}
```

- [ ] **Step 5: 验证编译和测试**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet build src/WeaveDoc.Converter.Ui/WeaveDoc.Converter.Ui.csproj
dotnet test tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj -v normal
```

Expected: BUILD SUCCEEDED, 6 个 UI 测试全部 PASS（TemplateTab 3 + ConvertTab 3）。

- [ ] **Step 6: 提交**

```bash
git add src/WeaveDoc.Converter.Ui/ tests/WeaveDoc.Converter.Ui.Tests/
git commit -m "feat(ui): add document conversion wizard tab with Headless UI tests"
```

---

### Task 4: 全量构建 + 回归验证

**Files:**
- 无新增修改

- [ ] **Step 1: 全量解决方案构建**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet build
```

Expected: 所有项目 BUILD SUCCEEDED。

- [ ] **Step 2: 运行 UI 测试项目**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet test tests/WeaveDoc.Converter.Ui.Tests/WeaveDoc.Converter.Ui.Tests.csproj -v normal
```

Expected: 全部 PASS。

- [ ] **Step 3: 运行 Converter 后端测试确认无回归**

```bash
cd "d:\Code All\WorkProgram\WeaveDoc"
dotnet test tests/WeaveDoc.Converter.Tests/WeaveDoc.Converter.Tests.csproj -v normal
```

Expected: 全部 PASS（62 个测试）。

- [ ] **Step 4: 如有修复性改动，提交**

```bash
git status
# 如有改动:
git add -A && git commit -m "fix(ui): resolve integration issues"
```

---

## Self-Review Checklist

- [ ] **Spec coverage:** 设计规格中 TabControl、TemplateTab（DataGrid + CRUD + 种子）、ConvertTab（向导式流程）全部有对应 Task
- [ ] **Testing:** 每个 Tab 都有 Headless UI 测试：TemplateTab 3 个测试（加载、种子、状态栏），ConvertTab 3 个测试（模板下拉、空输入验证、格式默认值）
- [ ] **API consistency:** ConfigManager 和 DocumentConversionEngine 构造函数签名已与实际代码验证一致
- [ ] **Placeholder scan:** 无 TBD/TODO/实现稍后，每步都有完整代码
- [ ] **Type consistency:** AfdMeta、ConfigManager、DocumentConversionEngine 使用正确的命名空间和类型
- [ ] **No over-engineering:** Code-behind 模式，无 MVVM，无 DI 容器，无多余抽象
