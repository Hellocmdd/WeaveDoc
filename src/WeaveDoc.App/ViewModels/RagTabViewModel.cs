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

public sealed record LocalLlamaModelItem(string Name, string Status, string Size, string Path);

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
    private LocalLlamaModelItem? _selectedLocalLlamaModel;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChatBackendScopeSummary)));
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

    public string LocalLlamaServerModel => _service.LlamaServerModel;

    public string LocalLlamaServerEndpoint => _service.LlamaServerEndpoint;

    public IReadOnlyList<LocalLlamaModelItem> LocalLlamaServerModels => DiscoverLocalLlamaServerModels();

    public bool HasLocalLlamaServerModels => LocalLlamaServerModels.Count > 0;

    public bool HasNoLocalLlamaServerModels => !HasLocalLlamaServerModels;

    public LocalLlamaModelItem? SelectedLocalLlamaModel
    {
        get => _selectedLocalLlamaModel;
        set
        {
            if (SetProperty(ref _selectedLocalLlamaModel, value))
            {
                OnPropertyChanged(nameof(CanDeleteSelectedLocalLlamaModel));
            }
        }
    }

    public bool CanDeleteSelectedLocalLlamaModel => SelectedLocalLlamaModel is not null;

    public string ChatBackendScopeSummary => IsCloudProviderSelected
        ? "云 API 只接管 Chat LLM；Embedding 与 reranker 仍使用本地 models/ 下的 GGUF 模型。"
        : "本地 llama-server 提供 Chat LLM；Embedding 与 reranker 同样使用本地模型。";

    public string LocalEmbeddingModelSummary
    {
        get
        {
            var fileName = Environment.GetEnvironmentVariable("RAG_EMBEDDING_MODEL_FILE");
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "bge-m3.gguf";
            }

            return $"{fileName.Trim()} · 本地 embedding · 启动时加载";
        }
    }

    public string LocalRerankerModelSummary
    {
        get
        {
            var enabled = Environment.GetEnvironmentVariable("RAG_RERANKER_ENABLED");
            if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase))
            {
                return "已关闭 · 本地 reranker";
            }

            var model = Environment.GetEnvironmentVariable("RAG_RERANKER_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "bge-reranker-v2-m3";
            }

            return $"{model.Trim()} · 本地 reranker · 按需启动";
        }
    }

    public string LocalModelStoragePath => Path.Combine(_service.WorkspaceRoot, "models");

    public string LocalMemorySummary => "加载会准备 embedding、语料索引、当前 Chat 后端，并按配置启动本地 reranker；卸载会释放本地模型与 llama-server 进程。";

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

    public async Task LoadModelsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _service.CloudSettings = _cloudSettings;
        IsBusy = true;
        StatusText = IsCloudProviderSelected
            ? "正在加载本地检索模型，并检查云端 Chat LLM..."
            : "正在加载本地检索模型，并启动 llama-server...";
        try
        {
            lock (_initializationGate)
            {
                _initializationTask = null;
            }

            _isInitialized = false;
            var initialized = await EnsureInitializedAsync();
            if (initialized)
            {
                RefreshCorpusState();
                RefreshModelManagementState();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UnloadModels()
    {
        if (IsBusy)
        {
            StatusText = "请等待当前操作完成后再卸载模型。";
            return;
        }

        StopGenerating();
        _service.UnloadModels();
        lock (_initializationGate)
        {
            _initializationTask = null;
        }

        _isInitialized = false;
        CorpusFiles.Clear();
        LastRankedChunks = [];
        LastContextChunks = [];
        SourceText = string.Empty;
        RetrievalDebugText = _service.LastRetrievalDebug;
        StatusText = "模型已卸载。";
        RefreshModelManagementState();
    }

    public void RefreshLocalModels()
    {
        RefreshModelManagementState();
        StatusText = "本地模型列表已刷新。";
    }

    public void ImportLocalChatModel(string sourcePath)
    {
        if (IsBusy)
        {
            StatusText = "请等待当前操作完成后再导入模型。";
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            StatusText = "请选择有效的 GGUF 模型文件。";
            return;
        }

        if (!Path.GetExtension(sourcePath).Equals(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "只能导入 .gguf 模型文件。";
            return;
        }

        Directory.CreateDirectory(LocalModelStoragePath);
        var targetPath = Path.Combine(LocalModelStoragePath, Path.GetFileName(sourcePath));
        if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        RefreshModelManagementState();
        StatusText = $"已导入模型：{Path.GetFileName(targetPath)}";
    }

    public void DeleteSelectedLocalChatModel()
    {
        if (IsBusy)
        {
            StatusText = "请等待当前操作完成后再删除模型。";
            return;
        }

        var selected = SelectedLocalLlamaModel;
        if (selected is null)
        {
            StatusText = "请先选择要删除的 Chat GGUF 模型。";
            return;
        }

        var modelsRoot = Path.GetFullPath(LocalModelStoragePath + Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(selected.Path);
        if (!fullPath.StartsWith(modelsRoot, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "不允许删除模型目录之外的文件。";
            return;
        }

        if (!File.Exists(fullPath))
        {
            StatusText = "模型文件不存在，列表已刷新。";
            RefreshModelManagementState();
            return;
        }

        File.Delete(fullPath);
        SelectedLocalLlamaModel = null;
        RefreshModelManagementState();
        StatusText = $"已删除模型：{selected.Name}";
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

    private IReadOnlyList<LocalLlamaModelItem> DiscoverLocalLlamaServerModels()
    {
        var modelsRoot = LocalModelStoragePath;
        if (!Directory.Exists(modelsRoot))
        {
            return [];
        }

        var configuredModel = ResolveConfiguredChatModelPath(modelsRoot);
        return Directory.EnumerateFiles(modelsRoot, "*.gguf", SearchOption.TopDirectoryOnly)
            .Where(IsLikelyChatModel)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var name = Path.GetFileName(path);
                var status = string.Equals(path, configuredModel, StringComparison.OrdinalIgnoreCase)
                    ? "当前配置"
                    : "可用";
                return new LocalLlamaModelItem(name, status, FormatFileSize(path), path);
            })
            .ToArray();
    }

    private void RefreshModelManagementState()
    {
        var previousPath = SelectedLocalLlamaModel?.Path;
        OnPropertyChanged(nameof(LocalLlamaServerModels));
        OnPropertyChanged(nameof(HasLocalLlamaServerModels));
        OnPropertyChanged(nameof(HasNoLocalLlamaServerModels));
        OnPropertyChanged(nameof(LocalLlamaServerModel));
        OnPropertyChanged(nameof(LocalLlamaServerEndpoint));
        OnPropertyChanged(nameof(LocalEmbeddingModelSummary));
        OnPropertyChanged(nameof(LocalRerankerModelSummary));
        OnPropertyChanged(nameof(LocalModelStoragePath));
        OnPropertyChanged(nameof(LocalMemorySummary));
        OnPropertyChanged(nameof(ActiveProviderSummary));
        OnPropertyChanged(nameof(ProviderBadgeText));
        OnPropertyChanged(nameof(ProviderBadgeToolTip));
        OnPropertyChanged(nameof(ChatBackendScopeSummary));

        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            SelectedLocalLlamaModel = LocalLlamaServerModels.FirstOrDefault(item =>
                string.Equals(item.Path, previousPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private string? ResolveConfiguredChatModelPath(string modelsRoot)
    {
        var explicitModelPath = Environment.GetEnvironmentVariable("LLAMA_SERVER_MODEL");
        if (!string.IsNullOrWhiteSpace(explicitModelPath))
        {
            var fullPath = Path.GetFullPath(explicitModelPath.Trim());
            return File.Exists(fullPath) ? fullPath : null;
        }

        var preferredPath = Path.Combine(modelsRoot, "Qwen3.5-4B-Q4_K_M.gguf");
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        return Directory.EnumerateFiles(modelsRoot, "*.gguf", SearchOption.TopDirectoryOnly)
            .Where(IsLikelyChatModel)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsLikelyChatModel(string path)
    {
        var fileName = Path.GetFileName(path).ToLowerInvariant();
        return !fileName.Contains("embedding", StringComparison.Ordinal)
            && !fileName.Contains("reranker", StringComparison.Ordinal)
            && !fileName.Contains("ranker", StringComparison.Ordinal)
            && !fileName.Contains("bge", StringComparison.Ordinal)
            && !fileName.Contains("minilm", StringComparison.Ordinal);
    }

    private static string FormatFileSize(string path)
    {
        var length = new FileInfo(path).Length;
        const double gib = 1024d * 1024d * 1024d;
        const double mib = 1024d * 1024d;
        return length >= gib
            ? $"{length / gib:0.0} GB"
            : $"{length / mib:0.0} MB";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
