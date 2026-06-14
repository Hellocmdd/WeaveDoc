using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

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
        Loaded += OnLoaded;
    }

    private void BuildTabStrip()
    {
        TabStrip.Children.Clear();
        foreach (var (tab, label) in Tabs)
        {
            var button = new Button
            {
                Classes = { "settings-tab" },
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
        SelectTab(_initialTab);
        await LoadTemplatesAsync();
        WireCloudApi();
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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
