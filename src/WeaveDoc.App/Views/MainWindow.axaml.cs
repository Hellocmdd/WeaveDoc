using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.MarkdownEditor.Services;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App.Views;

public partial class MainWindow : Window
{
    private const double DefaultAiPanelWidth = 300;
    private const double AiPanelMinWidth = 280;
    private const double SplitterWidth = 4;

    private readonly AppShellViewModel _viewModel;
    private readonly LocalAiService? _aiService;
    private double _lastExpandedAiPanelWidth = DefaultAiPanelWidth;

    public MainWindow() : this(null!, null!, null!) { }

    public MainWindow(ConfigManager? configManager, DocumentConversionEngine? engine, LocalAiService? aiService)
    {
        InitializeComponent();

        _aiService = aiService;

        var documentWorkspace = new DocumentWorkspaceViewModel(new MarkdownDocumentService());
        _viewModel = new AppShellViewModel(documentWorkspace, configManager, engine, aiService);
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnShellPropertyChanged;
        ApplyShellPalette(_viewModel.Theme);
        ApplyAiPanelLayout();
        UpdateStateClasses();
        Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Subscribe to PDF open request from PdfWorkspace (now hosted in the left sidebar)
        PdfWorkspaceControl.OpenPdfRequested += async (_, _) => await OpenDocumentAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnShellPropertyChanged;
        _viewModel.Dispose();
        _aiService?.Dispose();
        base.OnClosed(e);
    }

    private async void OnOpenDocumentClick(object? sender, RoutedEventArgs e)
        => await OpenDocumentAsync();

    private async void OnNewDocumentClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.DocumentWorkspace.NewAsync();
    }

    private async void OnSaveDocumentClick(object? sender, RoutedEventArgs e)
    {
        EditorWorkspaceControl.SyncEditorContentToWorkspace();
        var workspace = _viewModel.DocumentWorkspace;
        if (!workspace.HasDocument)
            return; // nothing to save

        // If the document has no file path (new unsaved document), prompt Save As.
        if (string.IsNullOrWhiteSpace(workspace.CurrentFilePath))
        {
            await SaveAsAsync();
            return;
        }

        await workspace.SaveAsync();
    }

    private async void OnExportDocumentClick(object? sender, RoutedEventArgs e)
    {
        EditorWorkspaceControl.SyncEditorContentToWorkspace();
        var workspace = _viewModel.DocumentWorkspace;

        var configManager = _viewModel.ConfigManager;
        var engine = _viewModel.ConversionEngine;
        if (configManager is null || engine is null)
            return;

        // Ensure the source is persisted to disk if a document is open but unsaved.
        var sourcePath = workspace.CurrentFilePath ?? string.Empty;
        if (workspace.HasDocument && string.IsNullOrWhiteSpace(sourcePath))
        {
            await SaveAsAsync();
            sourcePath = workspace.CurrentFilePath ?? string.Empty;
        }

        // Always open the dialog; if no source document is open it guides the user.
        var dialog = new ExportDialog(configManager, engine, _viewModel.RagTabViewModel, sourcePath);
        await dialog.ShowDialog(this);

        // If the user chose to open a converted PDF, display it in the workspace viewer.
        if (!string.IsNullOrWhiteSpace(dialog.PendingOpenPdfPath))
        {
            var pdfPath = dialog.PendingOpenPdfPath;
            var displayName = System.IO.Path.GetFileName(pdfPath);
            await PdfWorkspaceControl.ShowPdfAsync(pdfPath, displayName, isTemporary: false);
        }
    }

    private async void OnOpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        var configManager = _viewModel.ConfigManager;
        if (configManager is null)
            return;

        var dialog = new SettingsDialog(configManager, _viewModel.RagTabViewModel);
        await dialog.ShowDialog(this);
    }

    private async Task SaveAsAsync()
    {
        var workspace = _viewModel.DocumentWorkspace;
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存文档",
            SuggestedFileName = workspace.DisplayName,
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown 文档") { Patterns = ["*.md"] },
            ]
        });

        var file = result;
        if (file is null)
            return;

        var localPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        await workspace.SaveAsAsync(localPath);
    }

    private async Task OpenDocumentAsync()
    {
        var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开文档",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown 文档") { Patterns = ["*.md"] },
                new FilePickerFileType("PDF 文件") { Patterns = ["*.pdf"] },
                FilePickerFileTypes.All
            ]
        });

        var file = selected.FirstOrDefault();
        if (file == null)
            return;

        var name = file.Name ?? string.Empty;
        if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var result = await StorageFileOpenService.PreparePdfAsync(file).ConfigureAwait(true);
            if (!result.Succeeded)
                return;

            await PdfWorkspaceControl.ShowPdfAsync(result.FilePath, result.DisplayName, result.IsTemporary);
            return;
        }

        // Markdown (and any non-pdf document) → middle editor
        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
            await _viewModel.DocumentWorkspace.OpenAsync(localPath);
    }

    private void OnToggleAiPanelClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleAiPanel();
    }

    private void OnSelectAiChatTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Chat);
    }

    private void OnSelectAiLiteratureTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Literature);
    }

    private void OnSelectAiSnapshotTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Snapshot);
    }

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTheme();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppShellViewModel.IsAiPanelExpanded):
                ApplyAiPanelLayout();
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.EditorMode):
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.SelectedAiPanelTab):
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.Theme):
                ApplyShellPalette(_viewModel.Theme);
                UpdateStateClasses();
                break;
        }
    }

    private void ApplyAiPanelLayout()
    {
        var columns = ShellWorkspace.ColumnDefinitions;
        var rightSplitterColumn = columns[3];
        var aiPanelColumn = columns[4];

        if (_viewModel.IsAiPanelExpanded)
        {
            aiPanelColumn.MinWidth = AiPanelMinWidth;
            aiPanelColumn.Width = new GridLength(Math.Max(AiPanelMinWidth, _lastExpandedAiPanelWidth));
            rightSplitterColumn.MinWidth = SplitterWidth;
            rightSplitterColumn.Width = new GridLength(SplitterWidth);
            RightWorkspaceSplitter.IsVisible = true;
            AiAssistantPanelControl.IsVisible = true;
            return;
        }

        var currentWidth = aiPanelColumn.Width.Value >= AiPanelMinWidth
            ? aiPanelColumn.Width.Value
            : aiPanelColumn.ActualWidth;
        if (currentWidth >= AiPanelMinWidth)
        {
            _lastExpandedAiPanelWidth = currentWidth;
        }

        AiAssistantPanelControl.IsVisible = false;
        RightWorkspaceSplitter.IsVisible = false;
        rightSplitterColumn.MinWidth = 0;
        rightSplitterColumn.Width = new GridLength(0);
        aiPanelColumn.MinWidth = 0;
        aiPanelColumn.Width = new GridLength(0);
    }

    private void UpdateStateClasses()
    {
        SetActive(AiShellCommandButton, _viewModel.IsAiPanelExpanded);
        SetActive(AiChatCommandButton, _viewModel.IsAiChatTabSelected);
        SetActive(AiLiteratureCommandButton, _viewModel.IsAiLiteratureTabSelected);
        SetActive(AiSnapshotCommandButton, _viewModel.IsAiSnapshotTabSelected);
        SetActive(ThemeMenuButton, _viewModel.Theme == ShellThemeKind.Dark);

        var editModeButton = EditorWorkspaceControl.FindControl<Button>("EditModeButton");
        var previewModeButton = EditorWorkspaceControl.FindControl<Button>("PreviewModeButton");
        SetActive(editModeButton, _viewModel.IsEditModeSelected);
        SetActive(previewModeButton, _viewModel.IsPreviewModeSelected);

        SetActive(AiAssistantPanelControl.FindControl<Button>("AiChatTabButton"), _viewModel.IsAiChatTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiLiteratureTabButton"), _viewModel.IsAiLiteratureTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiSnapshotTabButton"), _viewModel.IsAiSnapshotTabSelected);
    }

    private static void SetActive(Button? button, bool isActive)
    {
        if (button is null)
        {
            return;
        }

        if (isActive)
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
            return;
        }

        button.Classes.Remove("active");
    }

    private static void ApplyShellPalette(ShellThemeKind theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = theme == ShellThemeKind.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        var palette = theme == ShellThemeKind.Dark ? DarkShellPalette : LightShellPalette;
        foreach (var (key, color) in palette)
        {
            SetBrushColor(application, key, color);
        }
    }

    private static void SetBrushColor(Application application, string key, string color)
    {
        if (application.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = Color.Parse(color);
            return;
        }

        application.Resources[key] = new SolidColorBrush(Color.Parse(color));
    }

    private static readonly IReadOnlyDictionary<string, string> DarkShellPalette = new Dictionary<string, string>
    {
        ["ShellBackgroundBrush"] = "#0D1117",
        ["ShellChromeBrush"] = "#161B22",
        ["ShellTitleBarBrush"] = "#0D1117",
        ["ShellPanelBrush"] = "#161B22",
        ["ShellCardBrush"] = "#21262D",
        ["ShellRaisedBrush"] = "#1B222D",
        ["ShellInputBrush"] = "#0F151D",
        ["ShellHoverBrush"] = "#21262D",
        ["ShellSelectedBrush"] = "#1D3557",
        ["ShellBorderBrush"] = "#30363D",
        ["ShellSubtleBorderBrush"] = "#21262D",
        ["ShellTextBrush"] = "#E6EDF3",
        ["ShellMutedTextBrush"] = "#8B949E",
        ["ShellDisabledTextBrush"] = "#6E7681",
        ["ShellAccentBrush"] = "#58A6FF",
        ["ShellAccentStrongBrush"] = "#1F6FEB",
        ["ShellSuccessBrush"] = "#3FB950",
        ["ShellWarningBrush"] = "#D29922",
        ["ShellEditorBackgroundBrush"] = "#0D1117",
        ["ShellEditorPanelBrush"] = "#161B22",
        ["ShellPaperWorkspaceBrush"] = "#21262D",
        // Constant-light foregrounds for dark-always zones (do not flip with theme).
        ["ShellOnDarkTextBrush"] = "#E6EDF3",
        ["ShellOnDarkMutedTextBrush"] = "#8B949E",
        ["ShellOnDarkDisabledTextBrush"] = "#6E7681"
    };

    private static readonly IReadOnlyDictionary<string, string> LightShellPalette = new Dictionary<string, string>
    {
        ["ShellBackgroundBrush"] = "#FFFFFF",
        ["ShellChromeBrush"] = "#F8F9FA",
        ["ShellTitleBarBrush"] = "#F8F9FA",
        ["ShellPanelBrush"] = "#F8F9FA",
        ["ShellCardBrush"] = "#FFFFFF",
        ["ShellRaisedBrush"] = "#F6F8FA",
        ["ShellInputBrush"] = "#FFFFFF",
        ["ShellHoverBrush"] = "#EFF3F6",
        ["ShellSelectedBrush"] = "#DDF4FF",
        ["ShellBorderBrush"] = "#D0D7DE",
        ["ShellSubtleBorderBrush"] = "#EAEEF2",
        ["ShellTextBrush"] = "#1C2128",
        ["ShellMutedTextBrush"] = "#57606A",
        ["ShellDisabledTextBrush"] = "#8B949E",
        ["ShellAccentBrush"] = "#0969DA",
        ["ShellAccentStrongBrush"] = "#0550AE",
        ["ShellSuccessBrush"] = "#1A7F37",
        ["ShellWarningBrush"] = "#9A6700",
        ["ShellEditorBackgroundBrush"] = "#F6F8FA",
        ["ShellEditorPanelBrush"] = "#FFFFFF",
        ["ShellPaperWorkspaceBrush"] = "#F6F8FA",
        // Compatibility aliases for views that still reference the old dark-surface names.
        ["ShellOnDarkTextBrush"] = "#1C2128",
        ["ShellOnDarkMutedTextBrush"] = "#57606A",
        ["ShellOnDarkDisabledTextBrush"] = "#8B949E"
    };
}
