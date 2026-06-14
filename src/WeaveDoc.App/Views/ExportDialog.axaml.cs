using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Afd.Models;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class ExportDialog : Window
{
    private readonly ConfigManager? _configManager;
    private readonly DocumentConversionEngine? _engine;
    private readonly RagTabViewModel? _ragViewModel;
    private readonly string _sourceMdPath;

    private AfdMeta? _selectedTemplate;
    private bool _isDocx = true;
    private PdfLayoutMode _pdfLayoutMode = PdfLayoutMode.SingleColumn;
    private bool _isConverting;

    /// <summary>
    /// Set when the user chooses to open a converted PDF in the workspace viewer.
    /// The owner (MainWindow) reads this after <see cref="ShowDialog"/> returns.
    /// </summary>
    public string? PendingOpenPdfPath { get; private set; }

    /// <summary>Design-time constructor.</summary>
    public ExportDialog() : this(null, null, null, string.Empty) { }

    public ExportDialog(ConfigManager? configManager, DocumentConversionEngine? engine, RagTabViewModel? ragViewModel, string sourceMdPath)
    {
        _configManager = configManager;
        _engine = engine;
        _ragViewModel = ragViewModel;
        _sourceMdPath = sourceMdPath ?? string.Empty;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static IBrush? Brush(string key) => Application.Current?.Resources[key] as IBrush;

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await LoadTemplatesAsync();
    }

    // ── Template list ──

    private async Task LoadTemplatesAsync()
    {
        TemplateListPanel.Children.Clear();
        _selectedTemplate = null;
        UpdateExportEnabled();

        if (_configManager is null)
        {
            TemplateEmptyText.IsVisible = true;
            TemplateEmptyText.Text = "Converter 服务未初始化，无法加载模板。";
            return;
        }

        var templates = await _configManager.ListTemplatesAsync();
        if (templates.Count == 0)
        {
            TemplateEmptyText.IsVisible = true;
            return;
        }

        TemplateEmptyText.IsVisible = false;

        for (var i = 0; i < templates.Count; i++)
        {
            var meta = templates[i];
            var isSelected = i == 0;
            if (isSelected)
                _selectedTemplate = meta;
            TemplateListPanel.Children.Add(BuildTemplateRow(meta, isSelected));
        }

        UpdateExportEnabled();
    }

    private Border BuildTemplateRow(AfdMeta meta, bool isSelected)
    {
        var radio = new TextBlock
        {
            Text = isSelected ? "●" : "○",
            FontSize = 12,
            Foreground = isSelected ? Brush("ShellAccentBrush") : Brush("ShellMutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var nameBlock = new TextBlock
        {
            Text = $"{meta.TemplateName}  ({meta.TemplateId} · v{meta.Version})",
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            Foreground = Brush("ShellTextBrush"),
        };
        var subBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(meta.Author) ? "排版模板" : $"作者：{meta.Author}",
            FontSize = 10,
            Foreground = Brush("ShellMutedTextBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        };

        var badge = new Border
        {
            Background = Brush("ShellSelectedBrush"),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "可导出",
                FontSize = 9,
                Foreground = Brush("ShellSuccessBrush"),
            },
        };

        var textStack = new StackPanel { Spacing = 0 };
        textStack.Children.Add(nameBlock);
        textStack.Children.Add(subBlock);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("16,*,Auto"),
            ColumnSpacing = 10,
        };
        Grid.SetColumn(radio, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(badge, 2);
        grid.Children.Add(radio);
        grid.Children.Add(textStack);
        grid.Children.Add(badge);

        var row = new Border
        {
            Classes = { "tpl-row" },
            Tag = meta,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid,
        };
        if (isSelected)
            row.Classes.Add("selected");

        row.PointerPressed += (_, _) => SelectTemplate(row);
        return row;
    }

    private void SelectTemplate(Border row)
    {
        if (row.Tag is not AfdMeta meta) return;

        foreach (var child in TemplateListPanel.Children)
        {
            if (child is not Border r) continue;
            r.Classes.Remove("selected");
            if (r.Child is Grid g && g.Children.Count > 0 && g.Children[0] is TextBlock radio)
            {
                var isThis = ReferenceEquals(r, row);
                radio.Text = isThis ? "●" : "○";
                radio.Foreground = isThis ? Brush("ShellAccentBrush") : Brush("ShellMutedTextBrush");
            }
        }

        row.Classes.Add("selected");
        _selectedTemplate = meta;
        UpdateExportEnabled();
    }

    // ── Format / layout toggles ──

    private void OnFormatDocxClick(object? sender, RoutedEventArgs e)
    {
        _isDocx = true;
        FormatDocxButton.Classes.Add("active");
        FormatPdfButton.Classes.Remove("active");
        PdfLayoutPanel.IsVisible = false;
    }

    private void OnFormatPdfClick(object? sender, RoutedEventArgs e)
    {
        _isDocx = false;
        FormatPdfButton.Classes.Add("active");
        FormatDocxButton.Classes.Remove("active");
        PdfLayoutPanel.IsVisible = true;
    }

    private void OnPdfSingleClick(object? sender, RoutedEventArgs e)
    {
        _pdfLayoutMode = PdfLayoutMode.SingleColumn;
        PdfSingleButton.Classes.Add("active");
        PdfTwoButton.Classes.Remove("active");
    }

    private void OnPdfTwoClick(object? sender, RoutedEventArgs e)
    {
        _pdfLayoutMode = PdfLayoutMode.TwoColumn;
        PdfTwoButton.Classes.Add("active");
        PdfSingleButton.Classes.Remove("active");
    }

    // ── Output path ──

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        var ext = _isDocx ? "docx" : "pdf";
        var suggestedName = string.IsNullOrWhiteSpace(_sourceMdPath)
            ? $"导出文档.{ext}"
            : $"{Path.GetFileNameWithoutExtension(_sourceMdPath)}.{ext}";

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择输出文件",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType(_isDocx ? "Word 文档" : "PDF 文档")
                {
                    Patterns = [_isDocx ? "*.docx" : "*.pdf"]
                },
            ],
        });

        if (file?.TryGetLocalPath() is { } localPath)
            OutputPathBox.Text = localPath;
    }

    // ── Manage templates ──

    private async void OnManageTemplatesClick(object? sender, RoutedEventArgs e)
    {
        if (_configManager is null) return;
        var settings = new SettingsDialog(_configManager, _ragViewModel, SettingsDialogTab.Template);
        await settings.ShowDialog(this);
        await LoadTemplatesAsync();
    }

    // ── Export ──

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_isConverting) return;
        if (_engine is null || _configManager is null)
        {
            SetStatus("Converter 服务未初始化", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_sourceMdPath) || !File.Exists(_sourceMdPath))
        {
            SetStatus("源文件不存在，请先保存文档", isError: true);
            return;
        }

        if (_selectedTemplate is null)
        {
            SetStatus("请选择排版模板", isError: true);
            return;
        }

        var format = _isDocx ? "docx" : "pdf";
        string? targetPath = null;
        if (!string.IsNullOrWhiteSpace(OutputPathBox.Text))
        {
            try
            {
                targetPath = ResolveOutputPath(OutputPathBox.Text.Trim(), format);
            }
            catch (ArgumentException ex)
            {
                SetStatus(ex.Message, isError: true);
                return;
            }
        }

        // Converting state
        _isConverting = true;
        ExportButton.IsEnabled = false;
        ProgressBar.IsVisible = true;
        LogBox.IsVisible = false;
        OpenInViewerButton.IsVisible = false;
        PendingOpenPdfPath = null;
        SetStatus("转换中…");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = await _engine.ConvertAsync(
                _sourceMdPath, _selectedTemplate.TemplateId, format, _pdfLayoutMode, cts.Token);

            if (result.Success)
            {
                var finalPath = result.OutputPath;
                if (targetPath is not null
                    && !string.Equals(result.OutputPath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? "");
                    if (File.Exists(result.OutputPath))
                        File.Move(result.OutputPath, targetPath, overwrite: true);
                    finalPath = targetPath;
                }

                SetStatus(BuildSuccessStatus(result, finalPath), isSuccess: true);

                // Offer to open PDF output directly in the workspace viewer.
                if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
                {
                    PendingOpenPdfPath = finalPath;
                    OpenInViewerButton.IsVisible = true;
                }
            }
            else
            {
                SetStatus("转换失败", isError: true);
                LogBox.Text = BuildFailureLog(_selectedTemplate, format, _sourceMdPath, result);
                LogBox.IsVisible = true;
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("转换超时", isError: true);
        }
        catch (Exception ex)
        {
            SetStatus("转换出错", isError: true);
            LogBox.Text = $"转换出错：{ex.Message}\n\n技术详情:\n{ex}";
            LogBox.IsVisible = true;
        }
        finally
        {
            _isConverting = false;
            ProgressBar.IsVisible = false;
            UpdateExportEnabled();
        }
    }

    private void UpdateExportEnabled()
    {
        ExportButton.IsEnabled = !_isConverting
            && _engine is not null
            && _configManager is not null
            && _selectedTemplate is not null;
    }

    private void SetStatus(string text, bool isError = false, bool isSuccess = false)
    {
        StatusLabel.Text = text;
        var brush = isError
            ? Brush("ShellWarningBrush")
            : isSuccess
                ? Brush("ShellSuccessBrush")
                : Brush("ShellMutedTextBrush");
        StatusLabel.Foreground = brush;
        StatusDot.Background = brush;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnOpenInViewerClick(object? sender, RoutedEventArgs e)
    {
        // PendingOpenPdfPath was set on successful PDF conversion; closing returns
        // control to MainWindow which reads it and opens the workspace viewer.
        if (!string.IsNullOrWhiteSpace(PendingOpenPdfPath))
            Close();
    }

    // ── Helpers (mirrors ConvertTab.axaml.cs) ──

    private static string ResolveOutputPath(string rawPath, string format)
    {
        if (rawPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("输出路径包含非法字符。");

        var fileName = Path.GetFileName(rawPath);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("输出文件名不能为空。");

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("输出文件名包含非法字符。");

        var expectedExtension = "." + format.ToLowerInvariant();
        var currentExtension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(currentExtension))
            return Path.Combine(Path.GetDirectoryName(rawPath) ?? "", fileName + expectedExtension);

        if (!string.Equals(currentExtension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(Path.GetDirectoryName(rawPath) ?? "", Path.GetFileNameWithoutExtension(fileName) + expectedExtension);

        return rawPath;
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
}
