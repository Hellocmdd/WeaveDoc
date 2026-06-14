using CommunityToolkit.Mvvm.ComponentModel;
using WeaveDoc.App.Services.Documents;

namespace WeaveDoc.App.ViewModels;

public sealed class DocumentWorkspaceViewModel : ObservableObject
{
    private const string EmptyDisplayName = "未打开 Markdown 文档";
    private const string NewDisplayName = "未命名文档";
    private const string EmptyStatusText = "未打开文档";

    private readonly IMarkdownDocumentService _documentService;
    private string? _currentFilePath;
    private string _displayName = EmptyDisplayName;
    private string _content = string.Empty;
    private string _previewHtml = string.Empty;
    private bool _hasDocument;
    private bool _isDirty;
    private string _statusText = EmptyStatusText;
    private string? _errorMessage;

    public DocumentWorkspaceViewModel(IMarkdownDocumentService documentService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set => SetProperty(ref _currentFilePath, value);
    }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string Content
    {
        get => _content;
        set => UpdateContent(value);
    }

    public string PreviewHtml
    {
        get => _previewHtml;
        private set => SetProperty(ref _previewHtml, value);
    }

    public bool HasDocument
    {
        get => _hasDocument;
        private set
        {
            if (SetProperty(ref _hasDocument, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public bool CanSave => HasDocument && IsDirty;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Task<bool> NewAsync(CancellationToken cancellationToken = default)
    {
        CurrentFilePath = null;
        DisplayName = NewDisplayName;
        SetProperty(ref _content, string.Empty, nameof(Content));
        PreviewHtml = string.Empty;
        HasDocument = true;
        IsDirty = false;
        ClearError();
        StatusText = "新建文档";
        return Task.FromResult(true);
    }

    public async Task<bool> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.ReadAsync(filePath, cancellationToken);
        if (!result.Succeeded)
        {
            SetFailure(result.ErrorMessage);
            return false;
        }

        ApplyDocument(result, isDirty: false);
        ClearError();
        StatusText = $"已打开 {DisplayName}";
        return true;
    }

    public void UpdateContent(string? content)
    {
        var normalizedContent = content ?? string.Empty;
        if (!SetProperty(ref _content, normalizedContent, nameof(Content)))
        {
            return;
        }

        MarkEdited();
    }

    public void MarkEdited()
    {
        if (!HasDocument)
        {
            return;
        }

        IsDirty = true;
        StatusText = $"已修改 {DisplayName}";
    }

    public bool RefreshPreview()
    {
        var previewResult = _documentService.CreatePreview(Content, CurrentFilePath);
        if (!previewResult.Succeeded)
        {
            SetFailure(previewResult.ErrorMessage);
            return false;
        }

        PreviewHtml = previewResult.PreviewHtml;
        ClearError();
        return true;
    }

    private System.Threading.CancellationTokenSource? _debounceCts;

    public async System.Threading.Tasks.Task<bool> DebouncedRefreshPreview(int delayMs = 300)
    {
        _debounceCts?.Cancel();
        _debounceCts = new System.Threading.CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, token);
            return RefreshPreview();
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            return false;
        }

        var result = await _documentService.SaveAsync(CurrentFilePath, Content, cancellationToken);
        if (!result.Succeeded)
        {
            SetFailure(result.ErrorMessage);
            return false;
        }

        ApplyDocument(result, isDirty: false);
        ClearError();
        StatusText = $"已保存 {DisplayName}";
        return true;
    }

    public async Task<bool> SaveAsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await _documentService.SaveAsync(filePath, Content, cancellationToken);
        if (!result.Succeeded)
        {
            SetFailure(result.ErrorMessage);
            return false;
        }

        ApplyDocument(result, isDirty: false);
        ClearError();
        StatusText = $"已保存 {DisplayName}";
        return true;
    }

    private void ApplyDocument(MarkdownDocumentResult result, bool isDirty)
    {
        CurrentFilePath = result.FilePath;
        DisplayName = string.IsNullOrWhiteSpace(result.DisplayName) ? EmptyDisplayName : result.DisplayName;
        SetProperty(ref _content, result.Content ?? string.Empty, nameof(Content));
        PreviewHtml = result.PreviewHtml ?? string.Empty;
        HasDocument = true;
        IsDirty = isDirty;
    }

    private void SetFailure(string? errorMessage)
    {
        var displayableError = string.IsNullOrWhiteSpace(errorMessage)
            ? "Markdown 文档操作失败。"
            : errorMessage;

        ErrorMessage = displayableError;
        StatusText = displayableError;
    }

    private void ClearError()
    {
        ErrorMessage = null;
    }
}
