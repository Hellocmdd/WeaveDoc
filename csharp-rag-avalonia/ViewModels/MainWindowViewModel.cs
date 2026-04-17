using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using RagAvalonia.Models;
using RagAvalonia.Services;

namespace RagAvalonia.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LocalAiService _service = new();
    private readonly List<ChatTurn> _history = [];
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private string _conversationText = string.Empty;
    private string _sourceText = string.Empty;
    private string _retrievalDebugText = "尚未执行检索。";
    private string _inputText = string.Empty;
    private string _newDocumentPath = string.Empty;
    private string? _selectedDocument;
    private string _statusText = "准备加载本地模型...";
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isDocumentPanelExpanded = true;

    public ObservableCollection<string> CorpusFiles { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ConversationText
    {
        get => _conversationText;
        private set => SetProperty(ref _conversationText, value);
    }

    public string SourceText
    {
        get => _sourceText;
        private set => SetProperty(ref _sourceText, value);
    }

    public string RetrievalDebugText
    {
        get => _retrievalDebugText;
        private set => SetProperty(ref _retrievalDebugText, value);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public string NewDocumentPath
    {
        get => _newDocumentPath;
        set => SetProperty(ref _newDocumentPath, value);
    }

    public string? SelectedDocument
    {
        get => _selectedDocument;
        set => SetProperty(ref _selectedDocument, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsDocumentPanelExpanded
    {
        get => _isDocumentPanelExpanded;
        private set
        {
            if (SetProperty(ref _isDocumentPanelExpanded, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentPanelToggleText)));
            }
        }
    }

    public string DocumentPanelToggleText => IsDocumentPanelExpanded ? "收起文档" : "展开文档";

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在加载 embedding 并连接 llama-server...";
        try
        {
            await _service.InitializeAsync();
            RefreshCorpusState();
            StatusText = $"已就绪：{_service.CorpusChunkCount} 个知识块，聊天模型通过 llama-server({_service.LlamaServerModel})。";
            AppendSystemMessage("本地模型已加载完成，可以开始提问。");
            _isInitialized = true;
        }
        catch (Exception exception)
        {
            StatusText = $"加载失败: {exception.Message}";
            AppendSystemMessage($"初始化失败: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsBusy)
        {
            return;
        }

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var question = InputText.Trim();
            InputText = string.Empty;

            AppendTurn(new ChatTurn("用户", question, true));
            IsBusy = true;
            StatusText = "正在检索上下文并生成回答...";

            var answer = await _service.AskAsync(question, _history);
            RetrievalDebugText = _service.LastRetrievalDebug;
            AppendTurn(new ChatTurn("助手", answer, false));
            StatusText = "回答完成。";
        }
        catch (Exception exception)
        {
            StatusText = $"生成失败: {exception.Message}";
            AppendTurn(new ChatTurn("系统", $"生成失败: {exception.Message}", false));
        }
        finally
        {
            IsBusy = false;
            _sendLock.Release();
        }
    }

    public void ToggleDocumentPanel()
    {
        IsDocumentPanelExpanded = !IsDocumentPanelExpanded;
    }

    public async Task AddDocumentAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var path = NewDocumentPath.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = "请先输入要添加的文档路径。";
            return;
        }

        IsBusy = true;
        StatusText = "正在添加文档并重建索引...";
        try
        {
            await _service.AddDocumentAsync(path);
            NewDocumentPath = string.Empty;
            RefreshCorpusState();
            StatusText = "文档添加成功，索引已刷新。";
        }
        catch (Exception exception)
        {
            StatusText = $"添加文档失败: {exception.Message}";
            AppendTurn(new ChatTurn("系统", $"添加文档失败: {exception.Message}", false));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddDocumentFromPathAsync(string path)
    {
        NewDocumentPath = path;
        await AddDocumentAsync();
    }

    public async Task RefreshCorpusAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在刷新文档索引...";
        try
        {
            await _service.ReloadCorpusAsync();
            RefreshCorpusState();
            StatusText = $"索引已刷新：{_service.CorpusFiles.Count} 个文件，{_service.CorpusChunkCount} 个知识块。";
        }
        catch (Exception exception)
        {
            StatusText = $"刷新索引失败: {exception.Message}";
            AppendTurn(new ChatTurn("系统", $"刷新索引失败: {exception.Message}", false));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearConversation()
    {
        if (IsBusy)
        {
            return;
        }

        _history.Clear();
        ConversationText = string.Empty;
        RetrievalDebugText = "尚未执行检索。";
        StatusText = "会话已清空。";
    }

    public async Task DeleteSelectedDocumentAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedDocument))
        {
            StatusText = "请先在文档列表中选择要删除的文件。";
            return;
        }

        IsBusy = true;
        StatusText = "正在删除文档并重建索引...";
        try
        {
            await _service.DeleteDocumentAsync(SelectedDocument);
            RefreshCorpusState();
            StatusText = "文档删除成功，索引已刷新。";
        }
        catch (Exception exception)
        {
            StatusText = $"删除文档失败: {exception.Message}";
            AppendTurn(new ChatTurn("系统", $"删除文档失败: {exception.Message}", false));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _service.Dispose();
        _sendLock.Dispose();
    }

    private void AppendTurn(ChatTurn turn)
    {
        _history.Add(turn);
        RebuildConversationText();
    }

    private void AppendSystemMessage(string message)
    {
        _history.Add(new ChatTurn("系统", message, false));
        RebuildConversationText();
    }

    private void RebuildConversationText()
    {
        var builder = new StringBuilder();
        foreach (var turn in _history)
        {
            builder.AppendLine($"[{turn.Role}]");
            builder.AppendLine(turn.Content);
            builder.AppendLine();
        }

        ConversationText = builder.ToString().TrimEnd();
    }

    private string BuildSourceText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"文档来源根目录: {_service.WorkspaceRoot}/doc");
        builder.AppendLine($"llama-server: {_service.LlamaServerEndpoint}");
        builder.AppendLine($"chat model: {_service.LlamaServerModel}");
        builder.AppendLine($"已索引文件数: {_service.CorpusFiles.Count}");
        builder.AppendLine();

        foreach (var file in _service.CorpusFiles)
        {
            builder.AppendLine(file);
        }

        return builder.ToString().TrimEnd();
    }

    private void RefreshCorpusState()
    {
        SourceText = BuildSourceText();
        RetrievalDebugText = _service.LastRetrievalDebug;

        CorpusFiles.Clear();
        foreach (var file in _service.CorpusFiles)
        {
            CorpusFiles.Add(file);
        }

        if (!string.IsNullOrWhiteSpace(SelectedDocument) && !CorpusFiles.Contains(SelectedDocument))
        {
            SelectedDocument = null;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
