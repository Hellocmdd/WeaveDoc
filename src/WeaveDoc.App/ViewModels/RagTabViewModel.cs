using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using WeaveDoc.Rag.Models;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App.ViewModels;

/// <summary>UI-facing projection of a retrieved chunk — decouples views from RAG service internals.</summary>
public sealed record RetrievalChunkItem(string Citation, string FilePath, string SectionTitle, string ContentKind, string Text);

public sealed class RagTabViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>Minimum time between streamed UI flushes, to avoid per-token re-render cost.</summary>
    private static readonly long StreamFlushIntervalTicks = TimeSpan.FromMilliseconds(40).Ticks;

    private readonly LocalAiService _service;
    private readonly CloudApiSettings _cloudSettings = CloudApiSettings.Load();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private string _sourceText = string.Empty;
    private string _retrievalDebugText = "尚未执行检索。";
    private string _inputText = string.Empty;
    private string _newDocumentPath = string.Empty;
    private string? _selectedDocument;
    private string _statusText = "知识库待初始化，应用启动后会自动准备。";
    private bool _isBusy;
    private bool _isInitialized;
    private Task<bool>? _initializationTask;
    private readonly object _initializationGate = new();
    private bool _isDocumentPanelExpanded = true;
    private IReadOnlyList<RetrievalChunkItem> _lastRankedChunks = [];
    private IReadOnlyList<RetrievalChunkItem> _lastContextChunks = [];
    private bool _lastUsedSparsePrefilter;
    private CancellationTokenSource? _sendCts;

    public RagTabViewModel(LocalAiService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _service.CloudSettings = _cloudSettings;
        Turns.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTurns));
            OnPropertyChanged(nameof(HasNoTurns));
        };
        CorpusFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasCorpus));
            OnPropertyChanged(nameof(HasNoCorpus));
            OnPropertyChanged(nameof(CorpusChunkCount));
        };
    }

    public ObservableCollection<string> CorpusFiles { get; } = [];

    /// <summary>Conversation turns, displayed as chat bubbles and snapshotted as RAG history.</summary>
    public ObservableCollection<ChatTurn> Turns { get; } = [];

    public bool HasTurns => Turns.Count > 0;

    public bool HasNoTurns => Turns.Count == 0;

    public bool HasCorpus => CorpusFiles.Count > 0;

    public bool HasNoCorpus => CorpusFiles.Count == 0;

    public event PropertyChangedEventHandler? PropertyChanged;

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
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                OnPropertyChanged(nameof(IsSendEnabled));
                OnPropertyChanged(nameof(IsActionButtonEnabled));
            }
        }
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
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsSendEnabled));
                OnPropertyChanged(nameof(IsActionButtonEnabled));
                OnPropertyChanged(nameof(SendButtonText));
            }
        }
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

    /// <summary>Ranked retrieval chunks from the last question — drives the 「快照」 tab cards.</summary>
    public IReadOnlyList<RetrievalChunkItem> LastRankedChunks
    {
        get => _lastRankedChunks;
        private set
        {
            if (SetProperty(ref _lastRankedChunks, value))
            {
                OnPropertyChanged(nameof(HasRankedChunks));
            }
        }
    }

    /// <summary>Context chunks actually fed to the model from the last question.</summary>
    public IReadOnlyList<RetrievalChunkItem> LastContextChunks
    {
        get => _lastContextChunks;
        private set => SetProperty(ref _lastContextChunks, value);
    }

    public bool HasRankedChunks => LastRankedChunks.Count > 0;

    public bool LastUsedSparsePrefilter
    {
        get => _lastUsedSparsePrefilter;
        private set => SetProperty(ref _lastUsedSparsePrefilter, value);
    }

    public int CorpusChunkCount => _service.CorpusChunkCount;

    /// <summary>True when a question can be sent (not busy, non-empty input).</summary>
    public bool IsSendEnabled => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    /// <summary>True when the action button should be clickable — to send, or to stop an in-flight stream.</summary>
    public bool IsActionButtonEnabled => IsGenerating || (!IsBusy && !string.IsNullOrWhiteSpace(InputText));

    public string SendButtonText => IsGenerating ? "停止" : "发送";

    public bool IsGenerating => _sendCts is not null;

    public int SelectedPanelTab
    {
        get => _selectedPanelTab;
        set
        {
            if (SetProperty(ref _selectedPanelTab, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDocumentsTabSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSettingsTabSelected)));
            }
        }
    }
    private int _selectedPanelTab;

    public bool IsDocumentsTabSelected => _selectedPanelTab == 0;
    public bool IsSettingsTabSelected => _selectedPanelTab == 1;

    public string ChatProvider
    {
        get => _cloudSettings.ChatProvider;
        set
        {
            if (_cloudSettings.ChatProvider != value)
            {
                _cloudSettings.ChatProvider = value;
                _service.CloudSettings = _cloudSettings;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChatProvider)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCloudProviderSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLocalProviderSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProviderSummary)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProviderBadgeText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProviderBadgeToolTip)));
            }
        }
    }

    public string CloudBaseUrl
    {
        get => _cloudSettings.CloudBaseUrl;
        set
        {
            _cloudSettings.CloudBaseUrl = value;
            _service.CloudSettings = _cloudSettings;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloudBaseUrl)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProviderSummary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProviderBadgeToolTip)));
        }
    }

    public string CloudApiKey
    {
        get => _cloudSettings.CloudApiKey;
        set
        {
            _cloudSettings.CloudApiKey = value;
            _service.CloudSettings = _cloudSettings;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloudApiKey)));
        }
    }

    public string CloudModel
    {
        get => _cloudSettings.CloudModel;
        set
        {
            _cloudSettings.CloudModel = value;
            _service.CloudSettings = _cloudSettings;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloudModel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveProviderSummary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProviderBadgeText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProviderBadgeToolTip)));
        }
    }

    public bool CloudEnableThinking
    {
        get => _cloudSettings.CloudEnableThinking;
        set
        {
            _cloudSettings.CloudEnableThinking = value;
            _service.CloudSettings = _cloudSettings;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloudEnableThinking)));
        }
    }

    public string CloudReasoningEffort
    {
        get => _cloudSettings.CloudReasoningEffort;
        set
        {
            _cloudSettings.CloudReasoningEffort = value;
            _service.CloudSettings = _cloudSettings;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloudReasoningEffort)));
        }
    }

    public bool IsCloudProviderSelected => _cloudSettings.ChatProvider == "cloud";

    public bool IsLocalProviderSelected => _cloudSettings.ChatProvider != "cloud";

    /// <summary>
    /// One-line description of the inference backend actually in effect, shown in the model-management
    /// banner so it is always obvious whether local llama-server or the cloud API is active.
    /// </summary>
    public string ActiveProviderSummary
    {
        get
        {
            if (IsCloudProviderSelected)
            {
                var model = string.IsNullOrWhiteSpace(CloudModel) ? "未配置模型" : CloudModel;
                var url = string.IsNullOrWhiteSpace(CloudBaseUrl) ? "未配置地址" : CloudBaseUrl;
                return $"云 API（OpenAI 兼容）· {model} · {url}";
            }

            return $"本地 llama-server · {_service.LlamaServerModel} · {_service.LlamaServerEndpoint}";
        }
    }

    public string ProviderBadgeText
    {
        get
        {
            if (IsCloudProviderSelected)
            {
                var model = string.IsNullOrWhiteSpace(CloudModel) ? "未配置模型" : CloudModel;
                return $"模型: 云端 · {model}";
            }

            return $"模型: 本地 · {_service.LlamaServerModel}";
        }
    }

    public string ProviderBadgeToolTip => IsCloudProviderSelected
        ? $"当前回答由云 API 提供：{ActiveProviderSummary}"
        : $"当前回答由本地 llama-server 提供：{_service.LlamaServerModel} ({_service.LlamaServerEndpoint})";

    /// <summary>Allowed values for <see cref="CloudReasoningEffort"/> (cloud thinking models).</summary>
    public string[] ReasoningEffortOptions { get; } = ["low", "medium", "high"];

    public async Task InitializeAsync()
    {
        await EnsureInitializedAsync();
    }

    private async Task<bool> EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return true;
        }

        Task<bool> initializationTask;
        lock (_initializationGate)
        {
            initializationTask = _initializationTask ??= InitializeCoreAsync();
        }

        var initialized = await initializationTask;
        if (!initialized)
        {
            lock (_initializationGate)
            {
                if (ReferenceEquals(_initializationTask, initializationTask))
                {
                    _initializationTask = null;
                }
            }
        }

        return initialized;
    }

    private async Task<bool> InitializeCoreAsync()
    {
        IsBusy = true;
        StatusText = "正在准备 RAG：加载 embedding 模型、扫描语料并连接聊天服务...";
        try
        {
            _service.CloudSettings = _cloudSettings;
            await _service.InitializeAsync();
            RefreshCorpusState();
            StatusText = $"已就绪：{_service.CorpusChunkCount} 个知识块，聊天模型: {_service.LlamaServerModel} ({_service.LlamaServerEndpoint})。";
            Turns.Add(new ChatTurn("系统", "模型已就绪，可以开始提问。", false));
            _isInitialized = true;
            return true;
        }
        catch (Exception exception)
        {
            StatusText = $"加载失败: {exception.Message}";
            Turns.Add(new ChatTurn("系统", $"初始化失败: {exception.Message}", false));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Streams the answer token-by-token into a live assistant bubble.</summary>
    public async Task SendAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

        await _sendLock.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                return;
            }

            var question = InputText.Trim();
            if (!await EnsureInitializedAsync())
            {
                return;
            }

            InputText = string.Empty;

            Turns.Add(new ChatTurn("用户", question, true));
            var history = Turns.ToArray();
            Turns.Add(new ChatTurn("助手", string.Empty, false));
            var assistantIndex = Turns.Count - 1;

            IsBusy = true;
            StatusText = "正在检索上下文并生成回答...";
            SetSendCancellationTokenSource(new CancellationTokenSource());

            var builder = new StringBuilder();
            var lastFlush = Stopwatch.GetTimestamp();
            var snapshotsRefreshed = false;

            try
            {
                await foreach (var chunk in _service.AskStreamAsync(question, history, _sendCts!.Token))
                {
                    if (chunk.Replace)
                    {
                        builder.Clear();
                        builder.Append(chunk.Text);
                        SetAssistantTurn(assistantIndex, chunk.Text);
                    }
                    else
                    {
                        builder.Append(chunk.Text);
                        var now = Stopwatch.GetTimestamp();
                        if (chunk.Text.Contains('\n') || (now - lastFlush) >= StreamFlushIntervalTicks)
                        {
                            SetAssistantTurn(assistantIndex, builder.ToString());
                            lastFlush = now;
                        }
                    }

                    if (!snapshotsRefreshed)
                    {
                        // Retrieval ran inside AskStreamAsync before the first token — snapshots are ready now.
                        RefreshRetrievalSnapshots();
                        RetrievalDebugText = _service.LastRetrievalDebug;
                        snapshotsRefreshed = true;
                    }
                }

                SetAssistantTurn(assistantIndex, builder.ToString().TrimEnd());
                StatusText = "回答完成。";
            }
            catch (OperationCanceledException)
            {
                var partial = builder.ToString().TrimEnd();
                SetAssistantTurn(assistantIndex, string.IsNullOrEmpty(partial) ? "（已停止）" : partial + "\n\n（已停止）");
                StatusText = "已停止生成。";
            }
            catch (Exception exception)
            {
                StatusText = $"生成失败: {exception.Message}";
                var partial = builder.ToString();
                SetAssistantTurn(assistantIndex, string.IsNullOrEmpty(partial)
                    ? $"生成失败: {exception.Message}"
                    : partial + $"\n\n生成失败: {exception.Message}");
            }
            finally
            {
                IsBusy = false;
                SetSendCancellationTokenSource(null);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Cancels an in-flight streamed answer (called by the 「停止」 button).</summary>
    public void StopGenerating()
    {
        _sendCts?.Cancel();
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
            var result = await _service.AddDocumentAsync(path);
            NewDocumentPath = string.Empty;
            RefreshCorpusState();
            StatusText = result.StatusMessage;
        }
        catch (Exception exception)
        {
            StatusText = $"添加文档失败: {exception.Message}";
            Turns.Add(new ChatTurn("系统", $"添加文档失败: {exception.Message}", false));
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
            Turns.Add(new ChatTurn("系统", $"刷新索引失败: {exception.Message}", false));
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

        Turns.Clear();
        RetrievalDebugText = "尚未执行检索。";
        LastRankedChunks = [];
        LastContextChunks = [];
        StatusText = "会话已清空。";
    }

    public void SaveCloudSettings()
    {
        if (IsBusy)
        {
            StatusText = "请等待当前操作完成后再保存设置。";
            return;
        }

        try
        {
            _cloudSettings.Save();
            _service.CloudSettings = _cloudSettings;
            StatusText = "云 API 设置已保存。";
        }
        catch (Exception exception)
        {
            StatusText = $"保存设置失败: {exception.Message}";
        }
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
            Turns.Add(new ChatTurn("系统", $"删除文档失败: {exception.Message}", false));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        // LocalAiService is injected — its lifecycle is owned by the creator (MainWindow/AppShell).
        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendLock.Dispose();
    }

    private void SetSendCancellationTokenSource(CancellationTokenSource? value)
    {
        if (ReferenceEquals(_sendCts, value))
        {
            return;
        }

        var previous = _sendCts;
        _sendCts = value;
        previous?.Dispose();
        OnPropertyChanged(nameof(IsGenerating));
        OnPropertyChanged(nameof(IsActionButtonEnabled));
        OnPropertyChanged(nameof(SendButtonText));
    }

    private void SetAssistantTurn(int index, string content)
    {
        if (index < 0 || index >= Turns.Count)
        {
            return;
        }

        Turns[index] = new ChatTurn("助手", content, false);
    }

    private string BuildSourceText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"文档来源根目录: {_service.WorkspaceRoot}/doc");
        builder.AppendLine($"chat model: {_service.LlamaServerModel}");
        builder.AppendLine($"chat endpoint: {_service.LlamaServerEndpoint}");
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

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CorpusChunkCount)));
        RefreshRetrievalSnapshots();
    }

    private void RefreshRetrievalSnapshots()
    {
        LastRankedChunks = _service.LastRankedChunkSnapshots
            .Select(ToItem)
            .ToArray();
        LastContextChunks = _service.LastContextChunkSnapshots
            .Select(ToItem)
            .ToArray();
        LastUsedSparsePrefilter = _service.LastUsedSparsePrefilter;
    }

    private static RetrievalChunkItem ToItem(LocalAiService.RetrievalChunkSnapshot snapshot)
        => new(snapshot.Citation, snapshot.FilePath, snapshot.SectionTitle, snapshot.ContentKind, snapshot.Text);

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
