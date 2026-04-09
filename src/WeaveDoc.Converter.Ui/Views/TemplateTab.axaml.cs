using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter.Afd;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

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

    public async Task LoadTemplatesAsync()
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
