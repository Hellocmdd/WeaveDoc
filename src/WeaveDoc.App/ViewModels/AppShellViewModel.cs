using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App.ViewModels;

public enum WorkspaceSidebarTabKind
{
    Documents,
    Settings
}

public enum EditorSurfaceMode
{
    Edit,
    Preview
}

public enum WorkspaceMode
{
    Markdown,
    Pdf
}

public enum ShellThemeKind
{
    Dark,
    Light
}

public enum AiPanelTabKind
{
    Chat,
    Literature,
    Snapshot
}

public sealed class AppShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ConfigManager? _configManager;
    private readonly DocumentConversionEngine? _engine;

    private WorkspaceSidebarTabKind _selectedSidebarTab = WorkspaceSidebarTabKind.Documents;
    private EditorSurfaceMode _editorMode = EditorSurfaceMode.Edit;
    private ShellThemeKind _theme = ShellThemeKind.Dark;
    private AiPanelTabKind _selectedAiPanelTab = AiPanelTabKind.Chat;
    private bool _isAiPanelExpanded = true;
    private WorkspaceMode _workspaceMode = WorkspaceMode.Markdown;
    private string _currentPdfPath = string.Empty;
    private string _currentPdfDisplayName = string.Empty;

    /// <summary>Design-time / fallback constructor.</summary>
    public AppShellViewModel()
        : this(new DocumentWorkspaceViewModel(new MarkdownDocumentService()), null, null, null)
    {
    }

    /// <summary>Full DI constructor — receives all backend services.</summary>
    public AppShellViewModel(
        DocumentWorkspaceViewModel documentWorkspace,
        ConfigManager? configManager,
        DocumentConversionEngine? engine,
        LocalAiService? aiService)
    {
        _configManager = configManager;
        _engine = engine;
        DocumentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        DocumentWorkspace.PropertyChanged += OnDocumentWorkspacePropertyChanged;
        RagTabViewModel = aiService is not null ? new RagTabViewModel(aiService) : null;
    }

    public DocumentWorkspaceViewModel DocumentWorkspace { get; }

    /// <summary>Converter service — used by ExportDialog / SettingsDialog.</summary>
    public ConfigManager? ConfigManager => _configManager;

    /// <summary>Conversion engine — used by ExportDialog.</summary>
    public DocumentConversionEngine? ConversionEngine => _engine;

    /// <summary>RAG view-model — created when <see cref="LocalAiService"/> is provided.</summary>
    public RagTabViewModel? RagTabViewModel { get; }

    public ObservableCollection<string> Documents { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public WorkspaceSidebarTabKind SelectedSidebarTab
    {
        get => _selectedSidebarTab;
        private set
        {
            if (SetProperty(ref _selectedSidebarTab, value))
            {
                OnPropertyChanged(nameof(IsDocumentsTabSelected));
                OnPropertyChanged(nameof(IsSettingsTabSelected));
            }
        }
    }

    public EditorSurfaceMode EditorMode
    {
        get => _editorMode;
        private set
        {
            if (SetProperty(ref _editorMode, value))
            {
                OnPropertyChanged(nameof(IsEditModeSelected));
                OnPropertyChanged(nameof(IsPreviewModeSelected));
                OnPropertyChanged(nameof(IsEditorEmptyStateVisible));
                OnPropertyChanged(nameof(IsMarkdownEditorVisible));
                OnPropertyChanged(nameof(IsPreviewEmptyStateVisible));
                OnPropertyChanged(nameof(IsMarkdownPreviewVisible));
                OnPropertyChanged(nameof(ModeStatusText));
            }
        }
    }

    public WorkspaceMode WorkspaceMode
    {
        get => _workspaceMode;
        private set
        {
            if (SetProperty(ref _workspaceMode, value))
            {
                OnPropertyChanged(nameof(IsMarkdownWorkspaceVisible));
                OnPropertyChanged(nameof(IsPdfWorkspaceVisible));
            }
        }
    }

    public string CurrentPdfPath
    {
        get => _currentPdfPath;
        private set => SetProperty(ref _currentPdfPath, value);
    }

    public string CurrentPdfDisplayName
    {
        get => _currentPdfDisplayName;
        private set => SetProperty(ref _currentPdfDisplayName, value);
    }

    public ShellThemeKind Theme
    {
        get => _theme;
        private set
        {
            if (SetProperty(ref _theme, value))
            {
                OnPropertyChanged(nameof(ThemeToggleText));
                OnPropertyChanged(nameof(ThemeStatusText));
            }
        }
    }

    public bool IsAiPanelExpanded
    {
        get => _isAiPanelExpanded;
        private set
        {
            if (SetProperty(ref _isAiPanelExpanded, value))
            {
                OnPropertyChanged(nameof(IsAiPanelCollapsed));
                OnPropertyChanged(nameof(AiPanelToggleText));
            }
        }
    }

    public AiPanelTabKind SelectedAiPanelTab
    {
        get => _selectedAiPanelTab;
        private set
        {
            if (SetProperty(ref _selectedAiPanelTab, value))
            {
                OnPropertyChanged(nameof(IsAiChatTabSelected));
                OnPropertyChanged(nameof(IsAiLiteratureTabSelected));
                OnPropertyChanged(nameof(IsAiSnapshotTabSelected));
                OnPropertyChanged(nameof(AiPanelTitleText));
                OnPropertyChanged(nameof(AiPanelEmptyStateText));
            }
        }
    }

    public bool IsAiPanelCollapsed => !IsAiPanelExpanded;
    public bool IsDocumentsTabSelected => SelectedSidebarTab == WorkspaceSidebarTabKind.Documents;
    public bool IsSettingsTabSelected => SelectedSidebarTab == WorkspaceSidebarTabKind.Settings;
    public bool IsEditModeSelected => EditorMode == EditorSurfaceMode.Edit;
    public bool IsPreviewModeSelected => EditorMode == EditorSurfaceMode.Preview;
    public bool IsEditorEmptyStateVisible => IsEditModeSelected && !DocumentWorkspace.HasDocument;
    public bool IsMarkdownEditorVisible => IsEditModeSelected && DocumentWorkspace.HasDocument;
    public bool IsPreviewEmptyStateVisible => IsPreviewModeSelected && !DocumentWorkspace.HasDocument;
    public bool IsMarkdownPreviewVisible => IsPreviewModeSelected && DocumentWorkspace.HasDocument;
    public bool IsAiChatTabSelected => SelectedAiPanelTab == AiPanelTabKind.Chat;
    public bool IsAiLiteratureTabSelected => SelectedAiPanelTab == AiPanelTabKind.Literature;
    public bool IsAiSnapshotTabSelected => SelectedAiPanelTab == AiPanelTabKind.Snapshot;
    public bool HasDocuments => DocumentWorkspace.HasDocument || Documents.Count > 0;
    public bool HasDocument => DocumentWorkspace.HasDocument;
    /// <summary>True when no document is open — drives the editor-tab placeholder visibility.</summary>
    public bool HasNoDocument => !DocumentWorkspace.HasDocument;
    public bool IsMarkdownWorkspaceVisible => WorkspaceMode == WorkspaceMode.Markdown;
    public bool IsPdfWorkspaceVisible => WorkspaceMode == WorkspaceMode.Pdf;

    public string CurrentDocumentTitle => DocumentWorkspace.HasDocument
        ? DocumentWorkspace.DisplayName
        : "未打开 Markdown 文档";

    public string CurrentDocumentSubtitle => DocumentWorkspace.HasDocument
        ? DocumentWorkspace.CurrentFilePath ?? string.Empty
        : "本地文档打开、保存和渲染能力待接入。";

    public string EmptyDocumentText => "暂无打开的文档";
    public string EmptyPreviewText => "暂无可预览内容";
    public string EmptyAiConversationText => "暂无问答记录";
    public string AiPanelTitleText => SelectedAiPanelTab switch
    {
        AiPanelTabKind.Literature => "文献辅助",
        AiPanelTabKind.Snapshot => "快照辅助",
        _ => "问答辅助"
    };
    public string AiPanelEmptyStateText => SelectedAiPanelTab switch
    {
        AiPanelTabKind.Literature => "暂无文献信息",
        AiPanelTabKind.Snapshot => "暂无快照",
        _ => EmptyAiConversationText
    };
    public string PendingCommandText => "待接入";
    public string AiPanelToggleText => IsAiPanelExpanded ? "收起辅助栏" : "展开辅助栏";
    public string ThemeToggleText => Theme == ShellThemeKind.Dark ? "浅色" : "深色";
    public string ThemeStatusText => Theme == ShellThemeKind.Dark ? "深色" : "浅色";
    public string ModeStatusText => EditorMode == EditorSurfaceMode.Edit ? "编辑" : "预览";
    public string StatusText => DocumentWorkspace.StatusText;

    public void SelectSidebarTab(WorkspaceSidebarTabKind tab)
    {
        SelectedSidebarTab = tab;
    }

    public void OpenPdfMode(string filePath, string displayName)
    {
        CurrentPdfPath = filePath;
        CurrentPdfDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? System.IO.Path.GetFileName(filePath)
            : displayName;
        WorkspaceMode = WorkspaceMode.Pdf;
        OnPropertyChanged(nameof(StatusText));
    }

    public void ClosePdfMode()
    {
        WorkspaceMode = WorkspaceMode.Markdown;
        CurrentPdfPath = string.Empty;
        CurrentPdfDisplayName = string.Empty;
    }

    public void SelectEditorMode(EditorSurfaceMode mode)
    {
        EditorMode = mode;
    }

    public void ToggleAiPanel()
    {
        IsAiPanelExpanded = !IsAiPanelExpanded;
    }

    public void SelectAiPanelTab(AiPanelTabKind tab)
    {
        SelectedAiPanelTab = tab;
    }

    public void ToggleTheme()
    {
        Theme = Theme == ShellThemeKind.Dark ? ShellThemeKind.Light : ShellThemeKind.Dark;
    }

    private void OnDocumentWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DocumentWorkspaceViewModel.HasDocument):
                OnPropertyChanged(nameof(HasDocuments));
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(HasNoDocument));
                OnPropertyChanged(nameof(CurrentDocumentTitle));
                OnPropertyChanged(nameof(CurrentDocumentSubtitle));
                OnPropertyChanged(nameof(IsEditorEmptyStateVisible));
                OnPropertyChanged(nameof(IsMarkdownEditorVisible));
                OnPropertyChanged(nameof(IsPreviewEmptyStateVisible));
                OnPropertyChanged(nameof(IsMarkdownPreviewVisible));
                break;
            case nameof(DocumentWorkspaceViewModel.DisplayName):
                OnPropertyChanged(nameof(CurrentDocumentTitle));
                break;
            case nameof(DocumentWorkspaceViewModel.CurrentFilePath):
                OnPropertyChanged(nameof(CurrentDocumentSubtitle));
                break;
            case nameof(DocumentWorkspaceViewModel.StatusText):
                OnPropertyChanged(nameof(StatusText));
                break;
        }
    }

    public void Dispose()
    {
        DocumentWorkspace.PropertyChanged -= OnDocumentWorkspacePropertyChanged;
        RagTabViewModel?.Dispose();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
