using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Views;

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
        FormatPdfBtn.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        FormatPdfBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA8, 0xC7));
        FormatPdfBtn.FontWeight = FontWeight.Normal;
    }

    private void OnFormatPdf(object? sender, RoutedEventArgs e)
    {
        _isDocx = false;
        FormatPdfBtn.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xA5));
        FormatPdfBtn.Foreground = new SolidColorBrush(Colors.White);
        FormatPdfBtn.FontWeight = FontWeight.SemiBold;
        FormatDocxBtn.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        FormatDocxBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA8, 0xC7));
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
        var customFileName = OutputFileNameBox.Text?.Trim();
        string? outputFileNameOverride = null;
        try
        {
            outputFileNameOverride = ResolveCustomOutputFileName(customFileName, format);
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, "#F5222D");
            return;
        }

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
                var outputFileName = outputFileNameOverride ?? Path.GetFileName(result.OutputPath);
                var outputPath = Path.Combine(outputDir, outputFileName);
                if (result.OutputPath != outputPath && File.Exists(result.OutputPath))
                    File.Move(result.OutputPath, outputPath, overwrite: true);

                SetStatus(BuildSuccessStatus(result, outputPath), "#52C41A");
                LogBox.IsVisible = false;
            }
            else
            {
                SetStatus("转换失败", "#F5222D");
                LogBox.Text = BuildFailureLog(selected, format, mdPath, result);
                LogBox.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            SetStatus("转换出错", "#F5222D");
            LogBox.Text = $"转换出错：{ex.Message}\n\n技术详情:\n{ex}";
            LogBox.IsVisible = true;
        }
        finally
        {
            ConvertButton.IsEnabled = true;
            ConvertButton.Content = "开始转换";
        }
    }

    private static string? ResolveCustomOutputFileName(string? customFileName, string format)
    {
        if (string.IsNullOrWhiteSpace(customFileName))
            return null;

        if (customFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || customFileName.Contains(Path.DirectorySeparatorChar)
            || customFileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("导出文件名包含非法字符，请只输入文件名，不要包含路径或系统保留字符。");
        }

        var expectedExtension = "." + format.ToLowerInvariant();
        var currentExtension = Path.GetExtension(customFileName);
        var nameWithoutExtension = string.IsNullOrEmpty(currentExtension)
            ? customFileName
            : Path.GetFileNameWithoutExtension(customFileName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            throw new ArgumentException("导出文件名不能为空。");

        var reservedNames = new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        if (reservedNames.Contains(nameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("导出文件名是系统保留名称，请换一个名称。");

        if (string.IsNullOrEmpty(currentExtension))
            return customFileName + expectedExtension;

        if (string.Equals(currentExtension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            return customFileName;

        return Path.GetFileNameWithoutExtension(customFileName) + expectedExtension;
    }

    private static string BuildFailureLog(AfdMeta selected, string format, string mdPath, ConversionResult result)
    {
        var text = $"转换失败：{result.ErrorMessage}\n\n模板: {selected.TemplateName} ({selected.TemplateId})\n格式: {format}\n输入: {mdPath}";
        if (!string.IsNullOrWhiteSpace(result.TechnicalDetails)
            && !string.Equals(result.TechnicalDetails.Trim(), result.ErrorMessage.Trim(), StringComparison.Ordinal))
        {
            text += $"\n\n技术详情:\n{result.TechnicalDetails}";
        }

        return text;
    }

    private static string BuildSuccessStatus(ConversionResult result, string outputPath)
    {
        if (!string.Equals(result.Format, "pdf", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(result.PdfConverterName))
        {
            return $"转换完成 — {outputPath}";
        }

        if (result.PdfConverterName.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
            return $"转换完成（使用 Syncfusion 兜底，字体保真度可能较低）— {outputPath}";

        return $"转换完成（PDF 引擎：{result.PdfConverterName}）— {outputPath}";
    }
}
