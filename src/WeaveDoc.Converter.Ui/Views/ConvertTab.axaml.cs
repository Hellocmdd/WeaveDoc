using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task LoadTemplatesAsync()
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
