using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using WeaveDoc.App.Services.ExportTemplates;
using Avalonia.Platform.Storage;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App.Views;

public enum SettingsDialogTab
{
    General,
    Models,
    Zotero,
    Template,
    Snapshot,
}

public partial class SettingsDialog : Window
{
    private readonly ConfigManager? _configManager;
    private readonly RagTabViewModel? _ragViewModel;
    private readonly SettingsDialogTab _initialTab;

    private SettingsDialogTab _selectedTab;
    private string? _pendingDeleteId;

    private static readonly (SettingsDialogTab Tab, string Label)[] Tabs =
    [
        (SettingsDialogTab.General, "通用"),
        (SettingsDialogTab.Models, "模型管理"),
        (SettingsDialogTab.Zotero, "Zotero"),
        (SettingsDialogTab.Template, "模板库"),
        (SettingsDialogTab.Snapshot, "快照策略"),
    ];

    /// <summary>Design-time constructor.</summary>
    public SettingsDialog() : this(null, null) { }

    public SettingsDialog(ConfigManager? configManager, RagTabViewModel? ragViewModel, SettingsDialogTab initialTab = SettingsDialogTab.General)
    {
        _configManager = configManager;
        _ragViewModel = ragViewModel;
        _initialTab = initialTab;
        _selectedTab = initialTab;

        InitializeComponent();
        BuildTabStrip();
        InitializeCustomTemplateOptions();
        Loaded += OnLoaded;
    }

    private void BuildTabStrip()
    {
        TabStrip.Children.Clear();
        foreach (var (tab, label) in Tabs)
        {
            var button = new Button
            {
                Classes = { "panel-tab" },
                Content = label,
                Tag = tab,
            };
            if (tab == _selectedTab)
                button.Classes.Add("active");
            button.Click += OnTabClick;
            TabStrip.Children.Add(button);
        }
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is SettingsDialogTab tab)
            SelectTab(tab);
    }

    private void SelectTab(SettingsDialogTab tab)
    {
        _selectedTab = tab;
        foreach (var child in TabStrip.Children)
        {
            if (child is Button btn && btn.Tag is SettingsDialogTab t)
            {
                if (t == tab) btn.Classes.Add("active");
                else btn.Classes.Remove("active");
            }
        }

        GeneralPanel.IsVisible = tab == SettingsDialogTab.General;
        ModelsPanel.IsVisible = tab == SettingsDialogTab.Models;
        ZoteroPanel.IsVisible = tab == SettingsDialogTab.Zotero;
        TemplatePanel.IsVisible = tab == SettingsDialogTab.Template;
        SnapshotPanel.IsVisible = tab == SettingsDialogTab.Snapshot;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DefaultWorkspaceTextBox.Text = WorkspacePaths.FindWorkspaceRoot();
        SelectTab(_initialTab);
        LoadSnapshotPolicySettings();
        await LoadTemplatesAsync();
        WireCloudApi();
    }

    private void LoadSnapshotPolicySettings()
    {
        var policy = SnapshotRetentionPolicy.Default;
        SnapshotAutoSaveDelayTextBox.Text = EditorWorkspace.AutoSaveDebounceMilliseconds.ToString();
        SnapshotIntervalTextBox.Text = policy.AutoSnapshotMinIntervalMinutes.ToString();
        SnapshotContentChangeThresholdTextBox.Text = policy.AutoSnapshotContentChangeThreshold.ToString();
        SnapshotRetentionCountTextBox.Text = policy.MaxSnapshotsPerDocument.ToString();
        SnapshotRetentionDaysTextBox.Text = policy.MaxRetentionDays.ToString();
        SnapshotDirectoryTextBox.Text = new WeaveDocUserDataPathProvider().GetSnapshotsRoot();
    }

    // ── Cloud API (模型管理 → 云 API 栏目) ──

    private void WireCloudApi()
    {
        if (_ragViewModel is null)
        {
            ModelsContent.IsVisible = false;
            ModelsPlaceholder.IsVisible = true;
            return;
        }

        ModelsPlaceholder.IsVisible = false;
        ModelsContent.IsVisible = true;
        ModelsContent.DataContext = _ragViewModel;
    }

    private void OnSelectLocalProvider(object? sender, RoutedEventArgs e)
    {
        if (_ragViewModel is { } vm)
        {
            vm.ChatProvider = "llama_server";
        }
    }

    private void OnSelectCloudProvider(object? sender, RoutedEventArgs e)
    {
        if (_ragViewModel is { } vm)
        {
            vm.ChatProvider = "cloud";
        }
    }

    private void OnSaveCloudClick(object? sender, RoutedEventArgs e)
    {
        _ragViewModel?.SaveCloudSettings();
    }

    private async void OnLoadModelsClick(object? sender, RoutedEventArgs e)
    {
        if (_ragViewModel is { } vm)
        {
            await vm.LoadModelsAsync();
        }
    }

    private void OnUnloadModelsClick(object? sender, RoutedEventArgs e)
    {
        _ragViewModel?.UnloadModels();
    }

    private void OnRefreshLocalModelsClick(object? sender, RoutedEventArgs e)
    {
        _ragViewModel?.RefreshLocalModels();
    }

    private void OnDeleteLocalModelClick(object? sender, RoutedEventArgs e)
    {
        _ragViewModel?.DeleteSelectedLocalChatModel();
    }

    private async void OnImportLocalModelClick(object? sender, RoutedEventArgs e)
    {
        if (_ragViewModel is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 GGUF Chat 模型",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("GGUF 模型")
                {
                    Patterns = ["*.gguf"],
                    AppleUniformTypeIdentifiers = ["public.data"],
                    MimeTypes = ["application/octet-stream"],
                },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ragViewModel.ImportLocalChatModel(path);
        }
    }

    private async void OnBrowseModelDirectoryClick(object? sender, RoutedEventArgs e)
    {
        if (_ragViewModel is null)
        {
            return;
        }

        Directory.CreateDirectory(_ragViewModel.LocalModelStoragePath);
        OpenDirectory(_ragViewModel.LocalModelStoragePath);
        await Task.CompletedTask;
    }

    private static void OpenDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", path);
            return;
        }

        Process.Start("xdg-open", path);
    }

    // ── Template management (mirrors TemplateTab.axaml.cs) ──

    private async Task LoadTemplatesAsync()
    {
        if (_configManager is null)
        {
            StatusBar.Text = "Converter 服务未初始化";
            return;
        }

        var templates = await _configManager.ListTemplatesAsync();
        TemplateGrid.ItemsSource = templates;
        StatusBar.Text = $"共 {templates.Count} 个模板";
        DeleteButton.IsVisible = false;
        DeleteButton.Content = "删除选中模板";
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = TemplateGrid.SelectedItem as AfdMeta;
        DeleteButton.IsVisible = selected is not null;
        _pendingDeleteId = null;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) => await LoadTemplatesAsync();

    private async void OnSeedClick(object? sender, RoutedEventArgs e)
    {
        if (_configManager is null) return;
        await _configManager.EnsureSeedTemplatesAsync();
        await LoadTemplatesAsync();
    }

    private void InitializeCustomTemplateOptions()
    {
        TemplateBaseFontComboBox.ItemsSource = CustomExportTemplateOptionsCatalog.FontFamilies;
        TemplateBaseFontSizeComboBox.ItemsSource = CustomExportTemplateOptionsCatalog.FontSizes;
        TemplateLineSpacingComboBox.ItemsSource = CustomExportTemplateOptionsCatalog.LineSpacings;
        TemplateFirstLineIndentComboBox.ItemsSource = new OptionItem<TemplateFirstLineIndentPreset>[]
        {
            new OptionItem<TemplateFirstLineIndentPreset>("无", TemplateFirstLineIndentPreset.None),
            new OptionItem<TemplateFirstLineIndentPreset>("首行缩进 2 字符", TemplateFirstLineIndentPreset.TwoCharacters),
        };
        TemplatePagePresetComboBox.ItemsSource = new OptionItem<TemplatePagePreset>[]
        {
            new OptionItem<TemplatePagePreset>("A4（210 × 297 mm）", TemplatePagePreset.A4),
            new OptionItem<TemplatePagePreset>("A5（148 × 210 mm）", TemplatePagePreset.A5),
            new OptionItem<TemplatePagePreset>("Letter（216 × 279 mm）", TemplatePagePreset.Letter),
        };
        TemplateMarginPresetComboBox.ItemsSource = new OptionItem<TemplateMarginPreset>[]
        {
            new OptionItem<TemplateMarginPreset>("标准边距", TemplateMarginPreset.Standard),
            new OptionItem<TemplateMarginPreset>("窄边距", TemplateMarginPreset.Narrow),
            new OptionItem<TemplateMarginPreset>("宽边距", TemplateMarginPreset.Wide),
            new OptionItem<TemplateMarginPreset>("论文边距", TemplateMarginPreset.Thesis),
        };
        TemplateHeadingPresetComboBox.ItemsSource = new OptionItem<TemplateHeadingPreset>[]
        {
            new OptionItem<TemplateHeadingPreset>("学术论文", TemplateHeadingPreset.Academic),
            new OptionItem<TemplateHeadingPreset>("报告文档", TemplateHeadingPreset.Report),
            new OptionItem<TemplateHeadingPreset>("紧凑排版", TemplateHeadingPreset.Compact),
        };
        TemplateCodeFontComboBox.ItemsSource = CustomExportTemplateOptionsCatalog.CodeFontFamilies;
        TemplateCodeFontSizeComboBox.ItemsSource = CustomExportTemplateOptionsCatalog.CodeFontSizes;

        ResetCustomTemplateForm();
    }

    private void ResetCustomTemplateForm()
    {
        CustomTemplateNameTextBox.Text = "自定义导出模板";
        CustomTemplateDescriptionTextBox.Text = "用户自定义 DOCX/PDF 导出模板";
        TemplateBaseFontComboBox.SelectedIndex = 0;
        TemplateBaseFontSizeComboBox.SelectedItem = 12.0;
        TemplateLineSpacingComboBox.SelectedItem = 1.5;
        TemplateFirstLineIndentComboBox.SelectedIndex = 1;
        TemplatePagePresetComboBox.SelectedIndex = 0;
        TemplateMarginPresetComboBox.SelectedIndex = 0;
        TemplateHeadingPresetComboBox.SelectedIndex = 0;
        TemplateCodeFontComboBox.SelectedIndex = 0;
        TemplateCodeFontSizeComboBox.SelectedItem = 10.0;
    }

    private void OnCreateCustomTemplateClick(object? sender, RoutedEventArgs e)
    {
        ResetCustomTemplateForm();
        CustomTemplateEditorPanel.IsVisible = true;
        StatusBar.Text = "正在创建自定义模板";
    }

    private void OnCancelCustomTemplateClick(object? sender, RoutedEventArgs e)
    {
        CustomTemplateEditorPanel.IsVisible = false;
        StatusBar.Text = "已取消自定义模板";
    }

    private async void OnSaveCustomTemplateClick(object? sender, RoutedEventArgs e)
    {
        if (_configManager is null)
        {
            StatusBar.Text = "Converter 服务未初始化，无法保存模板";
            return;
        }

        try
        {
            var options = new CustomExportTemplateOptions
            {
                TemplateName = string.IsNullOrWhiteSpace(CustomTemplateNameTextBox.Text)
                    ? "自定义导出模板"
                    : CustomTemplateNameTextBox.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(CustomTemplateDescriptionTextBox.Text)
                    ? "用户自定义 DOCX/PDF 导出模板"
                    : CustomTemplateDescriptionTextBox.Text.Trim(),
                BaseFontFamily = GetSelectedString(TemplateBaseFontComboBox, "宋体"),
                BaseFontSize = GetSelectedDouble(TemplateBaseFontSizeComboBox, 12),
                LineSpacing = GetSelectedDouble(TemplateLineSpacingComboBox, 1.5),
                FirstLineIndentPreset = GetSelectedOption(TemplateFirstLineIndentComboBox, TemplateFirstLineIndentPreset.TwoCharacters),
                PagePreset = GetSelectedOption(TemplatePagePresetComboBox, TemplatePagePreset.A4),
                MarginPreset = GetSelectedOption(TemplateMarginPresetComboBox, TemplateMarginPreset.Standard),
                HeadingPreset = GetSelectedOption(TemplateHeadingPresetComboBox, TemplateHeadingPreset.Academic),
                CodeFontFamily = GetSelectedString(TemplateCodeFontComboBox, "Consolas"),
                CodeFontSize = GetSelectedDouble(TemplateCodeFontSizeComboBox, 10),
            };

            var template = CustomExportTemplateBuilder.Create(options);
            var templateId = CustomExportTemplateBuilder.CreateTemplateId(options.TemplateName);
            await _configManager.SaveTemplateAsync(templateId, template);
            CustomTemplateEditorPanel.IsVisible = false;
            await LoadTemplatesAsync();
            StatusBar.Text = $"已保存自定义模板：{options.TemplateName}";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"保存失败: {ex.Message}";
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (_configManager is null) return;

        var storage = StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 AFD 模板 JSON 文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var parser = new AfdParser();
            var template = parser.ParseJson(json);
            parser.Validate(template);

            var templateId = Path.GetFileNameWithoutExtension(file.Name);
            await _configManager.SaveTemplateAsync(templateId, template);
            await LoadTemplatesAsync();
        }
        catch (AfdParseException ex)
        {
            StatusBar.Text = $"导入失败: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"导入失败: {ex.Message}";
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_configManager is null) return;
        var selected = TemplateGrid.SelectedItem as AfdMeta;
        if (selected is null) return;

        if (_pendingDeleteId != selected.TemplateId)
        {
            _pendingDeleteId = selected.TemplateId;
            DeleteButton.Content = $"确认删除 \"{selected.TemplateName}\"?";
            return;
        }

        _pendingDeleteId = null;
        await _configManager.DeleteTemplateAsync(selected.TemplateId);
        await LoadTemplatesAsync();
    }

    // 无边框窗口（SystemDecorations=None）需手动提供标题栏拖拽，
    // 否则对话框只能停在 CenterOwner 初始位置无法移动。
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private static string GetSelectedString(ComboBox comboBox, string fallback) =>
        comboBox.SelectedItem?.ToString() ?? fallback;

    private static double GetSelectedDouble(ComboBox comboBox, double fallback) =>
        comboBox.SelectedItem is double value ? value : fallback;

    private static T GetSelectedOption<T>(ComboBox comboBox, T fallback) =>
        comboBox.SelectedItem is OptionItem<T> item ? item.Value : fallback;

    private sealed record OptionItem<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
}
