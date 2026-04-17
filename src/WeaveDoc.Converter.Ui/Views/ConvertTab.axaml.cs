using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.Converter.Ui.Views;

public partial class ConvertTab : UserControl
{
    private ConfigManager? _configManager;
    private DocumentConversionEngine? _engine;
    private bool _isDocx = true;

    public ConvertTab()
    {
        InitializeComponent();
        BrowseMdButton.Click += OnBrowseMd;
        BrowseOutputButton.Click += OnBrowseOutput;
        ConvertButton.Click += OnConvert;
        FormatDocxBtn.Click += OnFormatDocx;
        FormatPdfBtn.Click += OnFormatPdf;
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

    private void OnFormatDocx(object? sender, RoutedEventArgs e)
    {
        _isDocx = true;
        FormatDocxBtn.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xA5));
        FormatDocxBtn.Foreground = new SolidColorBrush(Colors.White);
        FormatDocxBtn.FontWeight = FontWeight.SemiBold;
        FormatPdfBtn.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF8, 0xFC));
        FormatPdfBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x80, 0x96));
        FormatPdfBtn.FontWeight = FontWeight.Normal;
    }

    private void OnFormatPdf(object? sender, RoutedEventArgs e)
    {
        _isDocx = false;
        FormatPdfBtn.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xA5));
        FormatPdfBtn.Foreground = new SolidColorBrush(Colors.White);
        FormatPdfBtn.FontWeight = FontWeight.SemiBold;
        FormatDocxBtn.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xF8, 0xFC));
        FormatDocxBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x71, 0x80, 0x96));
        FormatDocxBtn.FontWeight = FontWeight.Normal;
    }

    private void SetStatus(string text, string color)
    {
        StatusLabel.Text = text;
        var brush = new SolidColorBrush(Color.Parse(color));
        StatusLabel.Foreground = brush;
        StatusDot.Background = brush;
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
            SetStatus("请选择有效的 Markdown 文件", "#F5222D");
            return;
        }

        var selected = TemplateCombo.SelectedItem as AfdMeta;
        if (selected == null)
        {
            SetStatus("请选择模板", "#F5222D");
            return;
        }

        var outputDir = OutputDirBox.Text?.Trim();
        if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
        {
            SetStatus("请选择输出目录", "#F5222D");
            return;
        }

        var format = _isDocx ? "docx" : "pdf";

        // 转换中状态
        SetStatus("转换中...", "#1890FF");
        ConvertButton.IsEnabled = false;
        ConvertButton.Content = "转换中...";
        LogBox.IsVisible = false;

        try
        {
            var result = await _engine.ConvertAsync(mdPath, selected.TemplateId, format);

            if (result.Success)
            {
                var outputPath = Path.Combine(outputDir, Path.GetFileName(result.OutputPath));
                if (result.OutputPath != outputPath && File.Exists(result.OutputPath))
                    File.Move(result.OutputPath, outputPath, overwrite: true);

                SetStatus($"转换完成 — {outputPath}", "#52C41A");
                LogBox.IsVisible = false;
            }
            else
            {
                SetStatus("转换失败", "#F5222D");
                LogBox.Text = $"模板: {selected.TemplateName} ({selected.TemplateId})\n格式: {format}\n输入: {mdPath}\n\n{result.ErrorMessage}";
                LogBox.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            SetStatus("转换出错", "#F5222D");
            LogBox.Text = $"异常: {ex.Message}";
            LogBox.IsVisible = true;
        }
        finally
        {
            ConvertButton.IsEnabled = true;
            ConvertButton.Content = "开始转换";
        }
    }
}
