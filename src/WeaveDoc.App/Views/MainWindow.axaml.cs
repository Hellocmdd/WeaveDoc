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
    private readonly bool _autoInitializeRag;
    private double _lastExpandedAiPanelWidth = DefaultAiPanelWidth;

    public MainWindow() : this(null!, null!, null!, null!) { }

    public MainWindow(
        ConfigManager? configManager,
        DocumentConversionEngine? engine,
        LocalAiService? aiService,
        ILiteratureRepository? literatureRepository = null,
        bool autoInitializeRag = false)
    {
        InitializeComponent();

        _aiService = aiService;
        _autoInitializeRag = autoInitializeRag;

        var citationPreviewService = literatureRepository is not null
            ? new CitationPreviewService(literatureRepository)
            : null;
        var documentWorkspace = new DocumentWorkspaceViewModel(
            new MarkdownDocumentService(),
            new DocumentSnapshotService(),
            citationPreviewService);
        _viewModel = new AppShellViewModel(documentWorkspace, configManager, engine, aiService, literatureRepository);
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

        // Bridge the Literature tab's "insert citation" request to the editor.
        // MainWindow is the only component holding named references to both the
        // AI panel (right) and the editor (center); there is no AI-panel→editor
        // channel, so we route the citation key here.
        if (_viewModel.LiteratureViewModel is { } literatureVm)
        {
            literatureVm.CitationInsertRequested += OnCitationInsertRequested;
        }

        if (_autoInitializeRag)
        {
            _ = _viewModel.RagTabViewModel?.InitializeAsync();
        }
    }

    private void OnCitationInsertRequested(string citationKey)
    {
        EditorWorkspaceControl.InsertCitation(citationKey);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel.LiteratureViewModel is { } literatureVm)
        {
            literatureVm.CitationInsertRequested -= OnCitationInsertRequested;
        }
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

    private async void OnOpenVersionHistoryClick(object? sender, RoutedEventArgs e)
    {
        EditorWorkspaceControl.SyncEditorContentToWorkspace();
        var dialog = new VersionHistoryDialog(_viewModel.DocumentWorkspace);
        var restored = await dialog.ShowDialog<bool>(this);
        if (restored)
        {
            _viewModel.DocumentWorkspace.RefreshPreview();
        }
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

    private void OnSelectAiCorpusTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Corpus);
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
        SetActive(AiCorpusCommandButton, _viewModel.IsAiCorpusTabSelected);
        SetActive(AiSnapshotCommandButton, _viewModel.IsAiSnapshotTabSelected);
        SetActive(ThemeMenuButton, _viewModel.Theme == ShellThemeKind.Dark);

        var editModeButton = EditorWorkspaceControl.FindControl<Button>("EditModeButton");
        var previewModeButton = EditorWorkspaceControl.FindControl<Button>("PreviewModeButton");
        SetActive(editModeButton, _viewModel.IsEditModeSelected);
        SetActive(previewModeButton, _viewModel.IsPreviewModeSelected);

        SetActive(AiAssistantPanelControl.FindControl<Button>("AiChatTabButton"), _viewModel.IsAiChatTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiLiteratureTabButton"), _viewModel.IsAiLiteratureTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiCorpusTabButton"), _viewModel.IsAiCorpusTabSelected);
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

        // Primary glow. BoxShadow XAML literals crash the Avalonia 12 runtime
        // resource loader, so the per-theme glows are built imperatively here.
        // BoxShadows is a struct; box it ONCE into object so the active glow
        // and its per-theme source share the same reference (lets Assert.Same
        // hold and lets {DynamicResource ShellPrimaryGlow} observe the swap).
        object darkGlow = CreatePrimaryGlowDark();
        object lightGlow = CreatePrimaryGlowLight();
        application.Resources["ShellPrimaryGlowDark"] = darkGlow;
        application.Resources["ShellPrimaryGlowLight"] = lightGlow;
        application.Resources["ShellPrimaryGlow"] = theme == ShellThemeKind.Dark ? darkGlow : lightGlow;
    }

    private static BoxShadows CreatePrimaryGlowDark()
    {
        var outer = BoxShadow.Parse("0 0 1 0 #7C9CFF");
        var inner = BoxShadow.Parse("0 6 18 -4 #5B8DEF");
        return new BoxShadows(outer, new[] { inner });
    }

    private static BoxShadows CreatePrimaryGlowLight()
    {
        var outer = BoxShadow.Parse("0 0 1 0 #9A6B3E");
        var inner = BoxShadow.Parse("0 6 16 -6 #8A5C32");
        return new BoxShadows(outer, new[] { inner });
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
        ["ShellBackgroundBrush"] = "#0B0E14",
        ["ShellChromeBrush"] = "#11151E",
        ["ShellTitleBarBrush"] = "#0B0E14",
        ["ShellPanelBrush"] = "#0F131B",
        ["ShellCardBrush"] = "#171C26",
        ["ShellRaisedBrush"] = "#161B25",
        ["ShellInputBrush"] = "#0E131C",
        ["ShellHoverBrush"] = "#1A2030",
        ["ShellSelectedBrush"] = "#1E2540",
        ["ShellBorderBrush"] = "#212836",
        ["ShellSubtleBorderBrush"] = "#1A2030",
        ["ShellTextBrush"] = "#E6EDF3",
        ["ShellMutedTextBrush"] = "#8B95A7",
        ["ShellDisabledTextBrush"] = "#5C6577",
        ["ShellAccentBrush"] = "#7C9CFF",
        ["ShellAccentStrongBrush"] = "#6E92FF",
        // Accent-tinted hover background + primary hover lift. Buttons swap the
        // neutral ShellHoverBrush (shared with cards/panels) for these so the
        // pointer-over state reads as "alive" accent rather than flat grey.
        ["ShellAccentHoverBrush"] = "#1B2540",
        // Fully-transparent but RGB-matched to the hover tint. Using this as the
        // default Background/BorderBrush lets BrushTransition animate alpha only
        // (RGB stays put) so leaving hover never flashes through grey/white the
        // way a true Transparent (#00FFFFFF) source would.
        ["ShellAccentHoverGhostBrush"] = "#001B2540",
        ["ShellAccentHoverStrongBrush"] = "#82A4FF",
        ["ShellSuccessBrush"] = "#3FB950",
        ["ShellWarningBrush"] = "#D29922",
        ["ShellEditorBackgroundBrush"] = "#0B0E14",
        ["ShellEditorPanelBrush"] = "#0F131B",
        ["ShellPaperWorkspaceBrush"] = "#11151E",
        // Constant-light foregrounds for dark-always zones (do not flip with theme).
        ["ShellOnDarkTextBrush"] = "#E6EDF3",
        ["ShellOnDarkMutedTextBrush"] = "#8B95A7",
        ["ShellOnDarkDisabledTextBrush"] = "#5C6577"
    };

    private static readonly IReadOnlyDictionary<string, string> LightShellPalette = new Dictionary<string, string>
    {
        ["ShellBackgroundBrush"] = "#FAF8F3",
        ["ShellChromeBrush"] = "#F1EDE4",
        ["ShellTitleBarBrush"] = "#F4F1EA",
        ["ShellPanelBrush"] = "#F6F3EC",
        ["ShellCardBrush"] = "#FFFFFF",
        ["ShellRaisedBrush"] = "#FFFFFF",
        ["ShellInputBrush"] = "#FFFFFF",
        ["ShellHoverBrush"] = "#EFEAE0",
        ["ShellSelectedBrush"] = "#F0E6D6",
        ["ShellBorderBrush"] = "#E2DCD0",
        ["ShellSubtleBorderBrush"] = "#EDE8DD",
        ["ShellTextBrush"] = "#322E26",
        ["ShellMutedTextBrush"] = "#968D7B",
        ["ShellDisabledTextBrush"] = "#B5AB97",
        ["ShellAccentBrush"] = "#A8662C",
        ["ShellAccentStrongBrush"] = "#8A5328",
        // Accent-tinted hover background + primary hover lift (see dark palette).
        ["ShellAccentHoverBrush"] = "#F0E2CB",
        ["ShellAccentHoverGhostBrush"] = "#00F0E2CB",
        ["ShellAccentHoverStrongBrush"] = "#9C6233",
        ["ShellSuccessBrush"] = "#4A7C4E",
        ["ShellWarningBrush"] = "#B8860B",
        ["ShellEditorBackgroundBrush"] = "#FFFFFF",
        ["ShellEditorPanelBrush"] = "#FFFFFF",
        ["ShellPaperWorkspaceBrush"] = "#F6F3EC",
        ["ShellOnDarkTextBrush"] = "#322E26",
        ["ShellOnDarkMutedTextBrush"] = "#968D7B",
        ["ShellOnDarkDisabledTextBrush"] = "#B5AB97"
    };
}
