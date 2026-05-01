using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using LLama;
using LLama.Common;
using LLama.Native;
using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed class LocalAiService : IDisposable
{
    private const int EmbeddingContextSize = 8192;
    private const int EmbeddingSafetyMargin = 256;
    private const int MaxEmbeddingTokens = EmbeddingContextSize - EmbeddingSafetyMargin;
    private static readonly string[] SupportedDocumentExtensions = [".md", ".txt", ".json"];
    private static readonly string[] IgnoredCorpusRelativePrefixes = ["QA/"];

    private static readonly Regex QueryTokenRegex = new("[a-z0-9]+|[\\u4e00-\\u9fff]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CitationRegex = new("\\[(?:\\d+|[^\\[\\]\\r\\n|]{1,160}\\s\\|\\s[^\\[\\]\\r\\n|]{1,160}\\s\\|\\sc\\d+)\\]", RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new("(?<=[。！？!?])(?:\\s+)?|(?<=\\.)\\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingGreetingRegex = new("^(?:\\s*(?:你好|您好|嗨|哈喽|hello|hi|hey|请问|麻烦问一下|我想问一下|想请教一下|请教一下)\\s*[,，.。!！?？:：;；/+|、-]*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BoundaryNoiseRegex = new("^[\\s,，.。!！?？:：;；/+|、-]+|[\\s,，.。!！?？:：;；/+|、-]+$", RegexOptions.Compiled);
    private static readonly Regex ReferenceLineRegex = new("^(\\[?\\d+\\]?\\s*)?.{0,80}\\[J\\].*(\\d{4}|\\(\\d+\\)).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeadingLineRegex = new("^#{1,6}\\s", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarkdownImageRegex = new("!\\[[^\\]]*\\]\\([^\\)]*\\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new("\\[([^\\]]+)\\]\\([^\\)]*\\)", RegexOptions.Compiled);
    private static readonly Regex TechnicalEntityRegex = new("(?<![A-Za-z0-9])(?:[A-Z][A-Za-z0-9]*(?:[-+./_][A-Za-z0-9]+)*(?:\\s+[A-Z][A-Za-z0-9+./_-]*){0,3}|[A-Za-z]+(?:[-+./_][A-Za-z0-9]+)+)(?![A-Za-z0-9])", RegexOptions.Compiled);
    private static readonly Regex ChineseEntityRegex = new("[\\u4e00-\\u9fffA-Za-z0-9+./_-]{2,24}(?:模块|系统|平台|框架|架构|数据库|接口|协议|算法|控制器|传感器|芯片|处理器|显示屏|屏幕|设备|组件|服务|工具|引擎|模型|网络|电源|电机|阀|终端|客户端|服务器|层|端)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MetadataLineRegex = new("^(关键词|key words|摘要|abstract|中图分类号|文献标识码|文章编号|doi|图\\d+|表\\d+|参考文献)[:：]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingOutlineRegex = new("^\\s*(?:\\d+(?:\\.\\d+)*\\s+)+", RegexOptions.Compiled);
    private static readonly Regex JsonArrayIndexRegex = new("^\\[(\\d+)\\]$", RegexOptions.Compiled);
    private static readonly Regex UsageSubjectRegex = new("(?:^|[，。；;：:\\s])(?:在.+?[里中内]\\s*)?(?<subject>[a-z0-9\\-_/+\\.\\u4e00-\\u9fff]{2,24}?)(?:有?什么作用|有什么用途|有什么功能|有什么用|作用是什么|用途是什么|功能是什么)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> FocusStopWords =
    [
        "什么", "哪些", "哪个", "那个", "这个", "一下", "一些", "其中", "对于", "相关", "关于", "有关", "内容",
        "文档", "资料", "介绍", "说明", "总结", "概述", "一下子", "请问", "你好", "您好", "可以", "是否",
        "怎么", "如何", "为什么", "原因", "原理", "机制", "作用", "用途", "功能", "区别", "比较", "对比",
        "流程", "步骤", "实现", "定义", "概念", "方法", "情况", "一下吗", "一下呢",
        "补充要求", "详细", "具体", "展开", "继续", "细一点", "详细一点", "具体一点"
    ];
    private static readonly string[] ExplanationSignals = ["原因", "因为", "导致", "所以", "由于", "为了", "通过", "原理", "机制"];
    private static readonly string[] CompareSignals = ["区别", "不同", "差异", "相比", "而", "则", "一方面", "另一方面"];
    private static readonly string[] CompositionSignals = ["组成", "构成", "包括", "包含", "模块", "架构", "技术栈", "前后端", "配置", "条件", "环境", "硬件", "软件", "接口"];
    private static readonly string[] DefinitionSignals = ["是一种", "是指", "作为", "选用", "采用", "负责", "用于", "核心", "模块"];
    private static readonly string[] ProcedureSignals = ["步骤", "流程", "首先", "然后", "最后", "实现", "方法", "过程"];
    private static readonly string[] ModuleImplementationSignals = ["连接", "通信", "上传", "下发", "展示", "显示", "设定", "触发", "指令", "链路", "协议", "服务器", "app", "json"];
    private static readonly string[] UsageSignals = ["用于", "作用", "用途", "负责", "实现", "控制", "采集", "识别", "管理"];
    private static readonly string[] DetailSignals = ["详细", "具体", "展开", "细说", "详述", "全面", "细一点", "详细一点", "具体一点", "展开讲", "展开说", "多说", "讲讲", "说说"];
    private static readonly string[] SupplementalRequestSignals = ["补充要求", "详细一点", "具体一点", "展开一点", "展开说", "展开讲", "细一点", "继续", "再说", "多说", "讲讲", "说说"];
    private static readonly string[] EnglishMetadataSignals = ["英文标题", "英文摘要", "英文关键词", "english title", "english abstract", "english keywords"];
    private static readonly string[] CompositionQuestionSignals = ["组成", "构成", "包括哪些", "包含哪些", "由哪些", "有哪些部分", "有哪些模块", "硬件组成", "软件组成", "硬件条件", "软件条件", "系统配置", "部署环境", "实验环境", "运行环境", "实施条件", "技术栈", "总体架构", "架构和技术栈", "前后端分离", "采用了什么架构", "采用了哪些技术"];
    private static readonly string[] CompositionContextSignals = ["硬件", "软件", "架构", "技术栈", "前后端", "模块", "组成", "构成", "配置", "条件", "环境", "接口", "参数", "依赖"];
    private static readonly string[] TechnicalStackQuestionSignals = ["技术栈", "总体架构", "架构和技术栈", "前后端分离", "采用了哪些技术", "采用了什么架构"];
    private static readonly HashSet<string> GenericEntityStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "系统", "模块", "功能模块", "这个系统", "该系统", "本文", "文中", "论文", "研究", "设计", "实现", "方法",
        "主要模块", "总体架构", "系统架构", "技术栈", "组成", "包括", "包含", "数据", "信息", "内容", "平台", "工具"
    };
    private static readonly string[] NonCompositionAttributeSignals = ["效果", "性能", "表现", "体验", "评价", "口碑", "结论"];
    private static readonly string[] ProcedureQuestionSignals = ["如何", "步骤", "流程", "怎么做", "怎么实现", "怎么控制", "怎么完成", "怎么处理", "怎么设计", "怎么搭建", "怎么部署", "怎么进行", "怎么安装", "怎么使用", "怎么办"];
    private static readonly HashSet<string> JsonIgnoredFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "classificationnumber", "documentidentifier", "articlenumber", "doi"
    };
    private static readonly HashSet<string> JsonPriorityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "titleen", "title_en", "documenttitle", "document_title", "name", "nameen", "name_en",
        "englishtitle", "english_title",
        "abstract", "abstracten", "abstract_en", "summary", "summaryen", "summary_en", "overview",
        "englishabstract", "english_abstract",
        "keywords", "keywordsen", "keywords_en", "englishkeywords", "english_keywords",
        "content", "architecture", "workflow"
    };
    private static readonly string[] JsonArrayItemTitleFieldCandidates = ["title", "title_en", "titleen", "name", "name_en", "nameen", "label", "module", "section", "step", "stage", "type", "id"];
    private static readonly HashSet<string> JsonMetadataFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authors", "author", "authorinfo", "affiliation", "englishaffiliation",
        "funding", "references", "reference", "citations", "citation"
    };
    private static readonly string[] JsonTitleFieldCandidates = ["title", "title_en", "titleen", "documentTitle", "document_title", "name", "name_en", "nameen", "englishTitle", "english_title"];
    private static readonly string[] JsonSummaryFieldCandidates = ["abstract", "abstract_en", "abstracten", "summary", "summary_en", "summaryen", "overview", "englishAbstract", "english_abstract"];
    private static readonly string[] JsonKeywordFieldCandidates = ["keywords", "keywords_en", "keywordsen", "englishKeywords", "english_keywords"];

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly List<DocumentChunk> _chunks = [];
    private readonly List<IndexedChunk> _indexedChunks = [];
    private readonly List<string> _corpusFiles = [];
    private readonly Dictionary<string, int> _documentFrequency = new(StringComparer.Ordinal);

    private readonly RagOptions _options = RagOptions.LoadFromEnvironment();
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _corpusLock = new(1, 1);

    private bool _initialized;
    private string? _workspaceRoot;
    private LLamaWeights? _embeddingWeights;
    private LLamaEmbedder? _embedder;
    private float _avgDocumentLength;
    private IReadOnlyList<string> _activeDocumentFilePaths = [];

    public string EmbeddingModelPath => GetModelPath(_options.EmbeddingModelFile);

    public string WorkspaceRoot => _workspaceRoot ?? WorkspacePaths.FindWorkspaceRoot();

    public string DocumentRoot => Path.Combine(WorkspaceRoot, "doc");

    public string CachePath => Path.Combine(WorkspaceRoot, ".rag", "embedding-cache.json");

    public int CorpusChunkCount => _chunks.Count;

    public IReadOnlyList<string> CorpusFiles => _corpusFiles;

    public string LlamaServerEndpoint => _options.LlamaServerBaseUrl;

    public string LlamaServerModel => _options.ChatModel;

    public string LastRetrievalDebug { get; private set; } = "尚未执行检索。";

    public IReadOnlyList<RetrievalChunkSnapshot> LastRankedChunkSnapshots { get; private set; } = [];

    public IReadOnlyList<RetrievalChunkSnapshot> LastContextChunkSnapshots { get; private set; } = [];

    public EvidenceDebugSnapshot? LastEvidenceDebugSnapshot { get; private set; }

    public bool LastUsedSparsePrefilter { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _workspaceRoot = WorkspacePaths.FindWorkspaceRoot();
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;

            await LoadEmbeddingModelAsync(cancellationToken).ConfigureAwait(false);
            await ReloadCorpusInternalAsync(cancellationToken).ConfigureAwait(false);

            var chatClient = new LlamaServerChatClient(_httpClient, _options);
            await chatClient.EnsureServerAvailableAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<string> AskAsync(string question, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        var retrievalQuestion = NormalizeQuestionForRetrieval(question, history);
        if (string.IsNullOrWhiteSpace(retrievalQuestion))
        {
            LastRetrievalDebug = "当前输入被识别为寒暄语，未执行文档检索。";
            ClearLastRetrievalSnapshots();
            return "你好，我是本地文档问答助手。你可以直接提问文档内容，或先导入/刷新文档后再问。";
        }

        if (_embedder is null)
        {
            throw new InvalidOperationException("Embedding model is not ready.");
        }

        RetrievalResult retrieval;
        await _corpusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            retrieval = await FindRelevantChunksAsync(retrievalQuestion, _options.TopK, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _corpusLock.Release();
        }

        LastRetrievalDebug = BuildRetrievalDebugText(question, retrievalQuestion, retrieval);
        LastRankedChunkSnapshots = retrieval.RankedChunks.Select(CreateRetrievalChunkSnapshot).ToArray();
        LastContextChunkSnapshots = retrieval.ContextChunks.Select(CreateRetrievalChunkSnapshot).ToArray();
        LastEvidenceDebugSnapshot = CreateEvidenceDebugSnapshot(retrieval.EvidencePlan, retrieval.EvidenceSet);
        LastUsedSparsePrefilter = retrieval.UsedSparsePrefilter;

        if (retrieval.RankedChunks.Count == 0)
        {
            return "未检索到足够相关的本地文档内容。请补充更具体的问题，或确认文档来源中包含该主题。";
        }

        if (retrieval.RankedChunks[0].Score < _options.MinCombinedThreshold)
        {
            return "未检索到足够相关的本地文档内容。请补充更具体的问题，或确认文档来源中包含该主题。";
        }

        var queryProfile = BuildQueryProfile(retrievalQuestion);
        var topChunks = retrieval.ContextChunks;

        if (!string.IsNullOrWhiteSpace(retrieval.EvidenceSet.UnknownReason))
        {
            return retrieval.EvidenceSet.UnknownReason;
        }

        if (!retrieval.EvidenceSet.IsSufficient)
        {
            return retrieval.EvidenceSet.UnknownReason ?? "我不知道（当前文档未覆盖）。";
        }

        RememberActiveDocumentScope(retrieval.EvidenceSet);

        if (ShouldReturnUnknownForUnsupportedRequestedDocumentTopic(queryProfile, topChunks, retrieval.TargetFilePaths))
        {
            return "我不知道（当前文档未覆盖）。";
        }

        if (IsPaperChallengeAndFutureQuestion(queryProfile.OriginalQuestion))
        {
            var directAnswer = BuildPaperChallengeAndFutureFallbackAnswer(topChunks);
            if (!string.IsNullOrWhiteSpace(directAnswer))
            {
                return directAnswer;
            }
        }

        if (ShouldUseDirectExplanationFallback(queryProfile, topChunks))
        {
            var directAnswer = BuildExplanationFallbackAnswer(queryProfile.OriginalQuestion, topChunks);
            if (!string.IsNullOrWhiteSpace(directAnswer))
            {
                return directAnswer;
            }
        }

        if (queryProfile.Intent == "summary")
        {
            var summaryAnswer = IsPaperQuestion(queryProfile.OriginalQuestion)
                ? BuildPaperSummaryFallbackAnswer(topChunks, queryProfile.WantsDetailedAnswer)
                : BuildSummaryFallbackAnswer(topChunks, queryProfile.WantsDetailedAnswer);
            if (!string.IsNullOrWhiteSpace(summaryAnswer))
            {
                return summaryAnswer;
            }
        }

        if (queryProfile.Intent == "procedure")
        {
            var procedureAnswer = BuildProcedureFallbackAnswer(queryProfile.OriginalQuestion, topChunks);
            if (!string.IsNullOrWhiteSpace(procedureAnswer))
            {
                return procedureAnswer;
            }
        }

        if (queryProfile.Intent == "composition")
        {
            var compositionAnswer = BuildCompositionFallbackAnswer(queryProfile.OriginalQuestion, topChunks);
            if (!string.IsNullOrWhiteSpace(compositionAnswer))
            {
                return compositionAnswer;
            }
        }

        var prompt = BuildPrompt(retrievalQuestion, history, queryProfile, retrieval.RankedChunks, topChunks, retrieval.TargetFilePaths);
        var client = new LlamaServerChatClient(_httpClient, _options);
        var answer = await client.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        if (IsOffTopicOrMalformedAnswer(answer, retrievalQuestion, topChunks, retrieval.TargetFilePaths))
        {
            var repaired = await client.CompleteAsync(BuildRepairPrompt(retrievalQuestion, queryProfile, retrieval.RankedChunks, topChunks, retrieval.TargetFilePaths), cancellationToken).ConfigureAwait(false);
            if (!IsOffTopicOrMalformedAnswer(repaired, retrievalQuestion, topChunks, retrieval.TargetFilePaths))
            {
                return repaired;
            }

            if (ShouldReturnUnknownForUnsupportedRequestedDocumentTopic(queryProfile, topChunks, retrieval.TargetFilePaths))
            {
                return "我不知道（当前文档未覆盖）。";
            }

            return BuildExtractiveFallbackAnswer(retrievalQuestion, queryProfile, topChunks, retrieval.TargetFilePaths);
        }

        return answer;
    }

    public async Task<AddDocumentResult> AddDocumentAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        var fullSourcePath = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"文档不存在: {fullSourcePath}", fullSourcePath);
        }

        var extension = Path.GetExtension(fullSourcePath);
        if (!IsSupportedDocumentExtension(extension))
        {
            throw new InvalidOperationException("仅支持 .md、.txt 或 .json 文档。");
        }

        Directory.CreateDirectory(DocumentRoot);

        var rootFullPath = Path.GetFullPath(DocumentRoot + Path.DirectorySeparatorChar);
        if (fullSourcePath.StartsWith(rootFullPath, StringComparison.Ordinal))
        {
            await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
            var relativeExistingPath = Path.GetRelativePath(DocumentRoot, fullSourcePath).Replace('\\', '/');
            return new AddDocumentResult(relativeExistingPath, false, "文档已在知识库目录中，已直接刷新索引。");
        }

        var existingDuplicate = Directory.EnumerateFiles(DocumentRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSupportedDocumentExtension(Path.GetExtension(path)))
            .FirstOrDefault(path => FilesHaveSameContent(fullSourcePath, path));

        if (!string.IsNullOrWhiteSpace(existingDuplicate))
        {
            await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
            var relativeExistingPath = Path.GetRelativePath(DocumentRoot, existingDuplicate).Replace('\\', '/');
            return new AddDocumentResult(relativeExistingPath, false, $"知识库中已存在相同内容的文档：{relativeExistingPath}");
        }

        var fileName = Path.GetFileName(fullSourcePath);
        var targetPath = Path.Combine(DocumentRoot, fileName);
        if (File.Exists(targetPath))
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            targetPath = Path.Combine(DocumentRoot, $"{nameWithoutExtension}-{suffix}{extension}");
        }

        File.Copy(fullSourcePath, targetPath, overwrite: false);
        await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
        return new AddDocumentResult(Path.GetRelativePath(DocumentRoot, targetPath).Replace('\\', '/'), true, "文档添加成功，索引已刷新。");
    }

    public async Task DeleteDocumentAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(DocumentRoot, normalized));
        var rootFullPath = Path.GetFullPath(DocumentRoot + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(rootFullPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不允许删除文档目录之外的文件。");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"未找到文档: {normalized}", fullPath);
        }

        File.Delete(fullPath);
        await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReloadCorpusAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await ReloadCorpusInternalAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReloadCorpusInternalAsync(CancellationToken cancellationToken)
    {
        await _corpusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadCorpusAsync(cancellationToken).ConfigureAwait(false);
            LastRetrievalDebug = "尚未执行检索。";
            ClearLastRetrievalSnapshots();
        }
        finally
        {
            _corpusLock.Release();
        }
    }
    public void Dispose()
    {
        _embedder?.Dispose();
        _embeddingWeights?.Dispose();
        _httpClient.Dispose();
        _corpusLock.Dispose();
        _initializationLock.Dispose();
    }

    private async Task LoadEmbeddingModelAsync(CancellationToken cancellationToken)
    {
        var parameters = new ModelParams(EmbeddingModelPath)
        {
            ContextSize = EmbeddingContextSize,
            BatchSize = 512,
            UBatchSize = 512,
            Threads = Environment.ProcessorCount,
            PoolingType = LLamaPoolingType.Unspecified,
            UseMemorymap = true,
            GpuLayerCount = _options.EmbeddingGpuLayerCount,
            FlashAttention = true,
        };

        _embeddingWeights?.Dispose();
        try
        {
            _embeddingWeights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch when (_options.EmbeddingGpuLayerCount > 0)
        {
            parameters.GpuLayerCount = 0;
            _embeddingWeights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken).ConfigureAwait(false);
        }

        _embedder?.Dispose();
        _embedder = new LLamaEmbedder(_embeddingWeights, parameters);
    }

    private async Task LoadCorpusAsync(CancellationToken cancellationToken)
    {
        if (_embedder is null || _workspaceRoot is null)
        {
            throw new InvalidOperationException("Embedding model is not initialized.");
        }

        _chunks.Clear();
        _indexedChunks.Clear();
        _corpusFiles.Clear();
        _documentFrequency.Clear();

        var docRoot = Path.Combine(_workspaceRoot, "doc");
        Directory.CreateDirectory(docRoot);

        var embeddingCache = await EmbeddingCache.LoadAsync(CachePath, cancellationToken).ConfigureAwait(false);
        var cacheChanged = false;
        var activeCacheKeys = new HashSet<string>(StringComparer.Ordinal);

        var corpusFiles = Directory.EnumerateFiles(docRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSupportedDocumentExtension(Path.GetExtension(path)))
            .Where(path => ShouldIndexCorpusFile(docRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in corpusFiles)
        {
            var relativePath = Path.GetRelativePath(docRoot, file).Replace('\\', '/');
            _corpusFiles.Add(relativePath);

            var chunks = await LoadChunksFromDocumentAsync(file, relativePath, cancellationToken).ConfigureAwait(false);
            foreach (var chunk in chunks)
            {
                _chunks.Add(chunk);
            }
        }

        foreach (var chunk in _chunks)
        {
            var retrievalText = BuildChunkRetrievalText(chunk);
            var cacheKey = BuildChunkCacheKey(chunk);
            activeCacheKeys.Add(cacheKey);

            if (!embeddingCache.TryGet(cacheKey, out var embedding))
            {
                embedding = await GetEmbeddingVectorAsync(retrievalText, cancellationToken).ConfigureAwait(false);
                embeddingCache.Set(cacheKey, embedding);
                cacheChanged = true;
            }

            var tokenFrequency = BuildTokenFrequency(retrievalText);
            _indexedChunks.Add(new IndexedChunk(chunk, embedding, tokenFrequency, tokenFrequency.Values.Sum()));

            foreach (var token in tokenFrequency.Keys)
            {
                _documentFrequency[token] = _documentFrequency.TryGetValue(token, out var count) ? count + 1 : 1;
            }
        }

        _avgDocumentLength = _indexedChunks.Count == 0
            ? 0f
            : (float)_indexedChunks.Average(item => item.TokenCount);

        if (embeddingCache.PruneExcept(activeCacheKeys))
        {
            cacheChanged = true;
        }

        if (cacheChanged)
        {
            await embeddingCache.SaveAsync(CachePath, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<RetrievalResult> FindRelevantChunksAsync(string question, int count, CancellationToken cancellationToken)
    {
        if (_embedder is null)
        {
            throw new InvalidOperationException("Embedding model is not initialized.");
        }

        var queryProfile = BuildQueryProfile(question);
        var queryTokens = BuildRetrievalQueryTokens(question, queryProfile.RequestedDocumentTitle);
        var targetFilePaths = ApplyRequestedFileFormatPreference(question, queryProfile.Intent, ResolveRequestedDocumentFilePaths(queryProfile.RequestedDocumentTitle));
        if (targetFilePaths.Count == 0 && ShouldUseActiveDocumentScope(queryProfile))
        {
            targetFilePaths = ApplyRequestedFileFormatPreference(question, queryProfile.Intent, _activeDocumentFilePaths);
        }

        var evidencePlan = BuildEvidencePlan(queryProfile, targetFilePaths);
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var scopedIndexedChunks = targetFilePathSet is null
            ? _indexedChunks.ToArray()
            : _indexedChunks.Where(item => targetFilePathSet.Contains(item.Chunk.FilePath)).ToArray();

        var sparseComponents = scopedIndexedChunks
            .Select(indexed =>
            {
                var bm25Raw = ComputeBm25(queryTokens, indexed);
                var (keywordScore, hasDirectKeywordHit) = ComputeKeywordScore(queryTokens, BuildChunkRetrievalText(indexed.Chunk));
                var titleScore = ComputeTitleScore(queryProfile.FocusTerms, indexed.Chunk);
                var jsonStructureScore = ComputeJsonStructureScore(queryProfile.FocusTerms, indexed.Chunk);
                var noisePenalty = ComputeNoisePenalty(indexed.Chunk);

                return new SparseRetrievalComponent(indexed, bm25Raw, keywordScore, titleScore, jsonStructureScore, noisePenalty, hasDirectKeywordHit);
            })
            .ToArray();

        if (sparseComponents.Length == 0 && targetFilePathSet is not null)
        {
            sparseComponents = _indexedChunks
                .Select(indexed =>
                {
                    var bm25Raw = ComputeBm25(queryTokens, indexed);
                    var (keywordScore, hasDirectKeywordHit) = ComputeKeywordScore(queryTokens, BuildChunkRetrievalText(indexed.Chunk));
                    var titleScore = ComputeTitleScore(queryProfile.FocusTerms, indexed.Chunk);
                    var jsonStructureScore = ComputeJsonStructureScore(queryProfile.FocusTerms, indexed.Chunk);
                    var noisePenalty = ComputeNoisePenalty(indexed.Chunk);

                    return new SparseRetrievalComponent(indexed, bm25Raw, keywordScore, titleScore, jsonStructureScore, noisePenalty, hasDirectKeywordHit);
                })
                .ToArray();
        }

        var maxBm25 = sparseComponents.Length == 0 ? 1f : Math.Max(1e-6f, sparseComponents.Max(item => item.Bm25Raw));
        var sparseRanked = sparseComponents
            .Select(item =>
            {
                var bm25Normalized = item.Bm25Raw / maxBm25;
                var requestedDocumentScore = ComputeRequestedDocumentScore(targetFilePathSet, item.Indexed.Chunk.FilePath);
                var sparseScore = (bm25Normalized * _options.Bm25Weight)
                    + (item.KeywordScore * _options.KeywordWeight)
                    + (item.TitleScore * _options.TitleWeight)
                    + (item.JsonStructureScore * _options.JsonStructureWeight)
                    + (item.HasDirectKeywordHit ? _options.DirectKeywordBonus : 0f)
                    + requestedDocumentScore
                    - item.NoisePenalty;

                return item with
                {
                    Bm25Score = bm25Normalized,
                    SparseScore = sparseScore
                };
            })
            .OrderByDescending(item => item.SparseScore)
            .ThenByDescending(item => item.HasDirectKeywordHit)
            .ThenBy(item => item.Indexed.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Indexed.Chunk.Index)
            .ToArray();

        var hasMeaningfulSparseEvidence = sparseRanked.Any(item =>
            item.HasDirectKeywordHit
            || item.KeywordScore > 0f
            || item.TitleScore > 0f
            || item.JsonStructureScore > 0f
            || item.Bm25Raw > 0f);

        var semanticInputs = hasMeaningfulSparseEvidence
            ? sparseRanked.Take(Math.Min(_options.SparseCandidatePoolSize, sparseRanked.Length)).ToArray()
            : sparseRanked;

        var questionVector = await GetEmbeddingVectorAsync(question, cancellationToken).ConfigureAwait(false);
        var candidatePool = semanticInputs
            .Select(item =>
            {
                var semanticScore = CosineSimilarity(questionVector, item.Indexed.Embedding);
                var score = (semanticScore * _options.VectorWeight) + item.SparseScore;
                var requestedDocumentScore = ComputeRequestedDocumentScore(targetFilePathSet, item.Indexed.Chunk.FilePath);

                return new ScoredChunk(item.Indexed.Chunk, score, semanticScore, item.Bm25Score, item.KeywordScore, item.TitleScore, item.JsonStructureScore, 0f, 0f, 0f, item.HasDirectKeywordHit, requestedDocumentScore);
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .ToArray();

        var candidates = candidatePool
            .Take(Math.Min(GetRerankCandidatePoolSize(queryProfile), candidatePool.Length))
            .ToArray();

        var evidenceCandidateCount = Math.Max(count, Math.Max(_options.RerankerTopN, evidencePlan.RequiredSlots.Count * (queryProfile.WantsDetailedAnswer ? 3 : 2)));
        var ruleRerankCount = Math.Min(candidates.Length, evidenceCandidateCount);
        var ruleReranked = RerankCandidates(candidates, queryProfile, evidencePlan, ruleRerankCount);
        var globalLearnedRerank = await TryRerankForGlobalRankingAsync(question, ruleReranked, evidencePlan, ruleRerankCount, cancellationToken).ConfigureAwait(false);
        var evidenceRerank = evidencePlan.RequiredSlots.Count == 0
            ? globalLearnedRerank
            : await TryRerankWithinEvidenceGroupsAsync(question, ruleReranked, evidencePlan, ruleRerankCount, cancellationToken).ConfigureAwait(false);
        var ranked = globalLearnedRerank.RankedChunks;
        var evidenceSet = BuildEvidenceSet(evidenceRerank.RankedChunks, queryProfile, evidencePlan, targetFilePaths);
        var rerankerStatus = evidencePlan.RequiredSlots.Count == 0
            ? globalLearnedRerank.Status
            : $"global:{globalLearnedRerank.Status}; evidence:{evidenceRerank.Status}";
        return new RetrievalResult(
            ranked,
            evidenceSet.Chunks,
            queryProfile,
            evidencePlan,
            evidenceSet,
            sparseRanked.Length,
            semanticInputs.Length,
            hasMeaningfulSparseEvidence,
            globalLearnedRerank.Used || evidenceRerank.Used,
            rerankerStatus,
            targetFilePaths);
    }

    private async Task<float[]> GetEmbeddingVectorAsync(string text, CancellationToken cancellationToken)
    {
        if (_embedder is null)
        {
            throw new InvalidOperationException("Embedding model is not initialized.");
        }

        var embeddingText = PrepareTextForEmbedding(text);
        var embeddings = await _embedder.GetEmbeddings(embeddingText, cancellationToken).ConfigureAwait(false);
        return embeddings.FirstOrDefault() ?? Array.Empty<float>();
    }

    private int GetRerankCandidatePoolSize(QueryProfile queryProfile)
    {
        var multiplier = queryProfile.Intent switch
        {
            "procedure" => 3,
            "module_implementation" => 3,
            "module_list" => 3,
            "metadata" => 2,
            _ => 1
        };

        return Math.Clamp(_options.CandidatePoolSize * multiplier, _options.CandidatePoolSize, _options.SparseCandidatePoolSize);
    }

    private async Task<LearnedRerankResult> TryRerankWithLearnedModelAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        int count,
        CancellationToken cancellationToken)
    {
        if (!_options.RerankerEnabled)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "disabled");
        }

        if (candidates.Count <= 1)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "not enough candidates");
        }

        var endpoint = BuildRerankerEndpoint(_options.RerankerBaseUrl);
        var documents = candidates
            .Select(candidate => BuildRerankerDocumentText(candidate.Chunk))
            .ToArray();
        var payload = new RerankRequest(_options.RerankerModel, question, documents, Math.Min(count, candidates.Count));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.RerankerTimeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, $"http {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            var scores = ParseRerankScores(document.RootElement);
            if (scores.Count == 0)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "empty response");
            }

            var scoreByIndex = scores
                .Where(score => score.Index >= 0 && score.Index < candidates.Count)
                .GroupBy(score => score.Index)
                .ToDictionary(group => group.Key, group => group.Max(item => item.Score));

            if (scoreByIndex.Count == 0)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "no usable scores");
            }

            var ranked = candidates
                .Select((candidate, index) =>
                {
                    var relevance = scoreByIndex.TryGetValue(index, out var score) ? score : float.NegativeInfinity;
                    return new
                    {
                        Candidate = candidate,
                        Relevance = relevance,
                        OriginalRank = index
                    };
                })
                .OrderByDescending(item => item.Relevance)
                .ThenByDescending(item => item.Candidate.Score)
                .ThenBy(item => item.OriginalRank)
                .Take(count)
                .Select(item => item.Candidate)
                .ToArray();

            return new LearnedRerankResult(ranked, true, $"ok ({scoreByIndex.Count}/{candidates.Count})");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, exception.GetType().Name);
        }
    }

    private async Task<LearnedRerankResult> TryRerankWithinEvidenceGroupsAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        EvidencePlan evidencePlan,
        int count,
        CancellationToken cancellationToken)
    {
        if (!_options.RerankerEnabled || candidates.Count <= 1)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, !_options.RerankerEnabled ? "disabled" : "not enough candidates");
        }

        if (evidencePlan.RequiredSlots.Count == 0)
        {
            var sameKind = candidates
                .Where(candidate => IsChunkAllowedByEvidencePlan(evidencePlan, candidate.Chunk))
                .Take(Math.Max(count, _options.RerankerTopN))
                .ToArray();
            return await TryRerankWithLearnedModelAsync(question, sameKind.Length > 0 ? sameKind : candidates, count, cancellationToken).ConfigureAwait(false);
        }

        var selected = new List<ScoredChunk>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        var statusParts = new List<string>();
        var used = false;
        var perSlotLimit = evidencePlan.QueryProfile.WantsDetailedAnswer ? 2 : 1;

        foreach (var slot in evidencePlan.RequiredSlots)
        {
            var slotCandidates = candidates
                .Where(candidate => IsChunkAllowedByEvidencePlan(evidencePlan, candidate.Chunk))
                .Where(candidate => ChunkMatchesEvidenceSlot(candidate.Chunk, slot))
                .Take(Math.Max(perSlotLimit + 2, Math.Min(_options.RerankerTopN, 6)))
                .ToArray();
            if (slotCandidates.Length == 0)
            {
                continue;
            }

            var reranked = await TryRerankWithLearnedModelAsync(question, slotCandidates, perSlotLimit, cancellationToken).ConfigureAwait(false);
            used |= reranked.Used;
            statusParts.Add($"{slot.Name}:{reranked.Status}");
            foreach (var item in reranked.RankedChunks)
            {
                var key = BuildChunkKey(item.Chunk);
                if (usedKeys.Add(key))
                {
                    selected.Add(item);
                }
            }
        }

        foreach (var candidate in candidates.Where(candidate => IsChunkAllowedByEvidencePlan(evidencePlan, candidate.Chunk)))
        {
            if (selected.Count >= count)
            {
                break;
            }

            var key = BuildChunkKey(candidate.Chunk);
            if (usedKeys.Add(key))
            {
                selected.Add(candidate);
            }
        }

        if (selected.Count == 0)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), used, statusParts.Count == 0 ? "no evidence groups" : string.Join("; ", statusParts));
        }

        return new LearnedRerankResult(selected.Take(count).ToArray(), used, statusParts.Count == 0 ? "grouped fallback" : string.Join("; ", statusParts));
    }

    private async Task<LearnedRerankResult> TryRerankForGlobalRankingAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        EvidencePlan evidencePlan,
        int count,
        CancellationToken cancellationToken)
    {
        var allowed = candidates
            .Where(candidate => IsChunkAllowedByEvidencePlan(evidencePlan, candidate.Chunk))
            .Take(Math.Max(count, _options.RerankerTopN))
            .ToArray();

        var rankingInput = allowed.Length > 0 ? allowed : candidates;
        return await TryRerankWithLearnedModelAsync(question, rankingInput, count, cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildRerankerEndpoint(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return new Uri($"{normalized}/v1/rerank", UriKind.Absolute);
    }

    private static string BuildRerankerDocumentText(DocumentChunk chunk)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"file: {chunk.FilePath}");
        if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            builder.AppendLine($"section: {chunk.StructurePath}");
        }
        else if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            builder.AppendLine($"section: {chunk.SectionTitle}");
        }

        builder.AppendLine($"kind: {chunk.ContentKind}");
        builder.AppendLine(BuildChunkRetrievalText(chunk));
        return builder.ToString();
    }

    private static List<RerankScore> ParseRerankScores(JsonElement root)
    {
        var scores = new List<RerankScore>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(root, scores);
            return scores;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return scores;
        }

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(results, scores);
        }
        else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(data, scores);
        }

        return scores;
    }

    private static void ParseRerankScoreArray(JsonElement items, List<RerankScore> scores)
    {
        var fallbackIndex = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                fallbackIndex++;
                continue;
            }

            var index = TryGetIntProperty(item, "index")
                ?? TryGetIntProperty(item, "document_index")
                ?? fallbackIndex;
            var score = TryGetFloatProperty(item, "relevance_score")
                ?? TryGetFloatProperty(item, "score")
                ?? TryGetFloatProperty(item, "relevance");

            if (score.HasValue)
            {
                scores.Add(new RerankScore(index, score.Value));
            }

            fallbackIndex++;
        }
    }

    private static int? TryGetIntProperty(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.String when int.TryParse(value.GetString(), out var intValue) => intValue,
            _ => null
        };
    }

    private static float? TryGetFloatProperty(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetSingle(out var floatValue) => floatValue,
            JsonValueKind.String when float.TryParse(value.GetString(), out var floatValue) => floatValue,
            _ => null
        };
    }


    private static bool IsSupportedDocumentExtension(string extension)
    {
        return SupportedDocumentExtensions.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldIndexCorpusFile(string docRoot, string filePath)
    {
        var relativePath = Path.GetRelativePath(docRoot, filePath).Replace('\\', '/');
        return !IgnoredCorpusRelativePrefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool FilesHaveSameContent(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        using var leftStream = File.OpenRead(leftPath);
        using var rightStream = File.OpenRead(rightPath);
        using var sha256 = SHA256.Create();
        var leftHash = sha256.ComputeHash(leftStream);
        rightStream.Position = 0;
        var rightHash = sha256.ComputeHash(rightStream);
        return leftHash.AsSpan().SequenceEqual(rightHash);
    }

    private async Task<IReadOnlyList<DocumentChunk>> LoadChunksFromDocumentAsync(string filePath, string relativePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        var raw = await ReadDocumentContentAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return SplitIntoChunks(raw, Path.GetFileName(filePath), relativePath);
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return BuildChunksFromJson(document.RootElement, Path.GetFileName(filePath), relativePath);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"JSON 文档解析失败: {Path.GetFileName(filePath)}，{exception.Message}", exception);
        }
    }

    private static async Task<string> ReadDocumentContentAsync(string filePath, CancellationToken cancellationToken)
    {
        return await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<DocumentChunk> BuildChunksFromJson(JsonElement root, string sourceName, string relativePath)
    {
        var documentTitle = ExtractJsonTitle(root, Path.GetFileNameWithoutExtension(sourceName));
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        AppendJsonChunks(root, sourceName, relativePath, documentTitle, documentTitle, documentTitle, chunks, ref chunkIndex);

        if (chunks.Count > 0)
        {
            return chunks;
        }

        var fallbackText = ConvertJsonToText(root, documentTitle);
        return SplitIntoChunks(fallbackText, sourceName, relativePath);
    }

    private static string ConvertJsonToText(JsonElement root, string defaultTitle)
    {
        var builder = new StringBuilder();
        var title = ExtractJsonTitle(root, defaultTitle);
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        AppendJsonElement(builder, root, title, 0);
        return builder.ToString().Trim();
    }

    private static string ExtractJsonTitle(JsonElement root, string defaultTitle)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return defaultTitle;
        }

        foreach (var name in JsonTitleFieldCandidates)
        {
            if (TryGetPropertyByNormalizedName(root, name, out var value))
            {
                var text = JsonScalarToString(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return defaultTitle;
    }

    private void AppendJsonChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        if (IsIgnoredJsonStructure(structurePath))
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendJsonObjectChunks(element, sourceName, relativePath, documentTitle, sectionTitle, structurePath, chunks, ref chunkIndex);
                break;
            case JsonValueKind.Array:
                AppendJsonArrayChunks(element, sourceName, relativePath, documentTitle, sectionTitle, structurePath, chunks, ref chunkIndex);
                break;
            default:
                AddStructuredChunk(chunks, sourceName, relativePath, documentTitle, sectionTitle, structurePath, "body", ref chunkIndex, JsonScalarToString(element));
                break;
        }
    }

    private void AppendJsonObjectChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        var scalarLines = new List<string>();

        foreach (var property in element.EnumerateObject())
        {
            var childPath = CombineStructurePath(structurePath, property.Name);
            var childSection = GetLeafStructureSegment(childPath);

            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AppendJsonChunks(property.Value, sourceName, relativePath, documentTitle, childSection, childPath, chunks, ref chunkIndex);
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonIgnoredFieldNames))
            {
                continue;
            }

            if (property.Name.Equals("number", StringComparison.OrdinalIgnoreCase) && IsIgnoredJsonStructure(childPath))
            {
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonPriorityFieldNames) || value.Length > _options.ChunkSize / 2)
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AddStructuredChunk(
                    chunks,
                    sourceName,
                    relativePath,
                    documentTitle,
                    childSection,
                    childPath,
                    ResolveJsonContentKind(property.Name, childPath),
                    ref chunkIndex,
                    $"{property.Name}: {value}");
                continue;
            }

            var scalarLine = $"{property.Name}: {value}";
            if (ShouldFlushStructuredValues(scalarLines, scalarLine))
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
            }

            scalarLines.Add(scalarLine);
        }

        FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
    }

    private void AppendJsonArrayChunks(
        JsonElement element,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        List<DocumentChunk> chunks,
        ref int chunkIndex)
    {
        var scalarValues = new List<string>();
        var itemIndex = 1;

        foreach (var item in element.EnumerateArray())
        {
            var itemPath = CombineStructurePath(structurePath, ResolveJsonArrayItemPathSegment(item, itemIndex));
            var itemSection = ExtractJsonArrayItemSectionTitle(item, itemIndex);

            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AppendJsonChunks(item, sourceName, relativePath, documentTitle, itemSection, itemPath, chunks, ref chunkIndex);
            }
            else
            {
                var value = JsonScalarToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (ShouldFlushStructuredValues(scalarValues, value, bulletPrefixLength: 2))
                    {
                        FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                    }

                    scalarValues.Add(value);
                }
            }

            itemIndex++;
        }

        FlushJsonArrayScalarChunk(chunks, scalarValues, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
    }

    private void FlushJsonScalarChunk(
        List<DocumentChunk> chunks,
        List<string> scalarLines,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex)
    {
        if (scalarLines.Count == 0)
        {
            return;
        }

        AddStructuredChunk(
            chunks,
            sourceName,
            relativePath,
            documentTitle,
            sectionTitle,
            structurePath,
            ResolveJsonContentKind(GetLeafStructureSegment(structurePath), structurePath),
            ref chunkIndex,
            string.Join('\n', scalarLines));

        scalarLines.Clear();
    }

    private void FlushJsonArrayScalarChunk(
        List<DocumentChunk> chunks,
        List<string> scalarValues,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex)
    {
        if (scalarValues.Count == 0)
        {
            return;
        }

        AddStructuredChunk(
            chunks,
            sourceName,
            relativePath,
            documentTitle,
            sectionTitle,
            structurePath,
            ResolveJsonContentKind(GetLeafStructureSegment(structurePath), structurePath),
            ref chunkIndex,
            string.Join('\n', scalarValues.Select(value => $"- {value}")));

        scalarValues.Clear();
    }

    private void AddStructuredChunk(
        List<DocumentChunk> chunks,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        string contentKind,
        ref int chunkIndex,
        string text)
    {
        var normalizedText = text.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        if (normalizedText.Length > _options.ChunkSize)
        {
            foreach (var oversizedPart in SplitLargeParagraph(normalizedText))
            {
                chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, oversizedPart, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, contentKind));
            }

            return;
        }

        chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, normalizedText, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, contentKind));
    }

    private static string CombineStructurePath(string parentPath, string childSegment)
    {
        return string.IsNullOrWhiteSpace(parentPath) ? childSegment : $"{parentPath} > {childSegment}";
    }

    private static string BuildJsonArrayItemPathSegment(int itemIndex)
    {
        return $"第{itemIndex}项";
    }

    private bool ShouldFlushStructuredValues(IReadOnlyList<string> values, string nextValue, int bulletPrefixLength = 0)
    {
        if (values.Count == 0)
        {
            return false;
        }

        var currentLength = values.Sum(value => value.Length + bulletPrefixLength) + (values.Count - 1);
        var safeLimit = Math.Max(120, _options.ChunkSize - 24);
        return currentLength + nextValue.Length + bulletPrefixLength + 1 > safeLimit;
    }

    private static string ExtractJsonArrayItemSectionTitle(JsonElement item, int itemIndex)
    {
        var baseLabel = BuildJsonArrayItemPathSegment(itemIndex);
        if (item.ValueKind != JsonValueKind.Object)
        {
            return baseLabel;
        }

        if (TryExtractJsonArrayItemSemanticLabel(item, out var semanticLabel))
        {
            return semanticLabel;
        }

        foreach (var property in item.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                continue;
            }

            if (IsJsonFieldName(property.Name, JsonIgnoredFieldNames))
            {
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return $"{baseLabel} {TruncateJsonSectionLabel($"{property.Name}: {value}")}";
        }

        return baseLabel;
    }

    private static string ResolveJsonArrayItemPathSegment(JsonElement item, int itemIndex)
    {
        return TryExtractJsonArrayItemSemanticLabel(item, out var semanticLabel)
            ? semanticLabel
            : BuildJsonArrayItemPathSegment(itemIndex);
    }

    private static bool TryExtractJsonArrayItemSemanticLabel(JsonElement item, out string label)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            label = string.Empty;
            return false;
        }

        string? numberLabel = null;
        if (TryGetPropertyByNormalizedName(item, "number", out var numberValue))
        {
            var numberText = JsonScalarToString(numberValue);
            if (!string.IsNullOrWhiteSpace(numberText))
            {
                numberLabel = TruncateJsonSectionLabel(numberText);
            }
        }

        foreach (var fieldName in JsonArrayItemTitleFieldCandidates)
        {
            if (!TryGetPropertyByNormalizedName(item, fieldName, out var value))
            {
                continue;
            }

            var titleLabel = JsonScalarToString(value);
            if (string.IsNullOrWhiteSpace(titleLabel))
            {
                continue;
            }

            label = string.IsNullOrWhiteSpace(numberLabel)
                ? TruncateJsonSectionLabel(titleLabel)
                : TruncateJsonSectionLabel($"{numberLabel} {titleLabel}");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(numberLabel))
        {
            label = numberLabel;
            return true;
        }

        label = string.Empty;
        return false;
    }

    private static string TruncateJsonSectionLabel(string text)
    {
        var normalized = text.Trim().Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ');
        return normalized.Length <= 48 ? normalized : normalized[..48].TrimEnd() + "...";
    }

    private static string GetLeafStructureSegment(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return "正文";
        }

        var segments = structurePath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? structurePath : segments[^1];
    }

    private static bool IsIgnoredJsonStructure(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return false;
        }

        return structurePath.Contains("参考文献", StringComparison.Ordinal)
            || structurePath.Contains("references", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetadataStructurePath(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return false;
        }

        return structurePath.Contains("author", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("authors", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("affiliation", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("funding", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("acknowledgements", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("article_info", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("references", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("citations", StringComparison.OrdinalIgnoreCase)
            || structurePath.Contains("citation", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveJsonContentKind(string fieldNameOrPathLeaf, string structurePath)
    {
        if (IsMetadataStructurePath(structurePath))
        {
            return "metadata";
        }

        return GetJsonContentKind(fieldNameOrPathLeaf);
    }

    private static string GetJsonContentKind(string fieldNameOrPathLeaf)
    {
        if (string.IsNullOrWhiteSpace(fieldNameOrPathLeaf))
        {
            return "body";
        }

        var normalized = fieldNameOrPathLeaf
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (IsJsonFieldName(normalized, JsonTitleFieldCandidates))
        {
            return "title";
        }

        if (IsJsonFieldName(normalized, JsonSummaryFieldCandidates))
        {
            return "summary";
        }

        if (IsJsonFieldName(normalized, JsonKeywordFieldCandidates))
        {
            return "keyword";
        }

        if (normalized.Equals("content", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        if (IsJsonFieldName(normalized, JsonMetadataFieldNames))
        {
            return "metadata";
        }

        return "body";
    }

    private static void AppendJsonElement(StringBuilder builder, JsonElement element, string label, int depth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendJsonObject(builder, element, depth);
                break;
            case JsonValueKind.Array:
                AppendJsonArray(builder, element, label, depth);
                break;
            default:
                var text = JsonScalarToString(element);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text);
                    builder.AppendLine();
                }
                break;
        }
    }

    private static void AppendJsonObject(StringBuilder builder, JsonElement element, int depth)
    {
        var scalarLines = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                FlushJsonScalarLines(builder, scalarLines);
                AppendJsonHeading(builder, property.Name, depth + 1);
                AppendJsonElement(builder, property.Value, property.Name, depth + 1);
                continue;
            }

            var value = JsonScalarToString(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                scalarLines.Add($"{property.Name}: {value}");
            }
        }

        FlushJsonScalarLines(builder, scalarLines);
    }

    private static void AppendJsonArray(StringBuilder builder, JsonElement element, string label, int depth)
    {
        var index = 1;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                AppendJsonHeading(builder, $"{label}[{index}]", depth + 1);
                AppendJsonElement(builder, item, $"{label}[{index}]", depth + 1);
            }
            else
            {
                var value = JsonScalarToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    builder.AppendLine($"- {value}");
                }
            }

            index++;
        }

        if (index > 1)
        {
            builder.AppendLine();
        }
    }

    private static void AppendJsonHeading(StringBuilder builder, string label, int depth)
    {
        var headingDepth = Math.Min(6, depth + 1);
        builder.AppendLine($"{new string('#', headingDepth)} {label}");
        builder.AppendLine();
    }

    private static void FlushJsonScalarLines(StringBuilder builder, List<string> scalarLines)
    {
        if (scalarLines.Count == 0)
        {
            return;
        }

        foreach (var line in scalarLines)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
        scalarLines.Clear();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetPropertyByNormalizedName(JsonElement element, string propertyName, out JsonElement value)
    {
        var normalizedTarget = NormalizeJsonFieldName(propertyName);
        foreach (var property in element.EnumerateObject())
        {
            if (NormalizeJsonFieldName(property.Name).Equals(normalizedTarget, StringComparison.Ordinal))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsJsonFieldName(string value, IEnumerable<string> candidates)
    {
        var normalized = NormalizeJsonFieldName(value);
        return candidates.Any(candidate => NormalizeJsonFieldName(candidate).Equals(normalized, StringComparison.Ordinal));
    }

    private static string NormalizeJsonFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string JsonScalarToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string BuildChunkRetrievalText(DocumentChunk chunk)
    {
        var parts = new List<string>(8);
        AppendRetrievalPart(parts, chunk.DocumentTitle);
        AppendRetrievalPart(parts, chunk.StructurePath);
        AppendNormalizedStructureRetrievalParts(parts, chunk.StructurePath);
        AppendRetrievalPart(parts, chunk.SectionTitle);
        AppendRetrievalPart(parts, chunk.ContentKind is "body" or "" ? string.Empty : chunk.ContentKind);
        AppendRetrievalPart(parts, chunk.Text);
        return string.Join('\n', parts);
    }

    private static void AppendNormalizedStructureRetrievalParts(List<string> parts, string structurePath)
    {
        foreach (var segment in GetNormalizedStructureSegments(structurePath))
        {
            AppendRetrievalPart(parts, segment);
        }
    }

    private static void AppendRetrievalPart(List<string> parts, string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (parts.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        parts.Add(normalized);
    }

    private static string[] GetNormalizedStructureSegments(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return [];
        }

        return structurePath
            .Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeStructureSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeStructureSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        var trimmed = segment.Trim();
        var arrayIndexMatch = JsonArrayIndexRegex.Match(trimmed);
        if (arrayIndexMatch.Success && int.TryParse(arrayIndexMatch.Groups[1].Value, out var itemIndex))
        {
            return BuildJsonArrayItemPathSegment(itemIndex);
        }

        trimmed = LeadingOutlineRegex.Replace(trimmed, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? segment.Trim() : trimmed;
    }

    private Dictionary<string, int> BuildTokenFrequency(string text)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in EnumerateTokens(text))
        {
            frequency[token] = frequency.TryGetValue(token, out var count) ? count + 1 : 1;
        }

        return frequency;
    }

    private float ComputeBm25(IReadOnlyList<string> queryTokens, IndexedChunk chunk)
    {
        if (queryTokens.Count == 0 || chunk.TokenCount == 0 || _indexedChunks.Count == 0)
        {
            return 0f;
        }

        const float k1 = 1.5f;
        const float b = 0.75f;

        var score = 0d;
        foreach (var token in queryTokens)
        {
            if (!chunk.TokenFrequency.TryGetValue(token, out var tf) || tf == 0)
            {
                continue;
            }

            var df = _documentFrequency.TryGetValue(token, out var value) ? value : 0;
            var idf = Math.Log(1 + ((_indexedChunks.Count - df + 0.5d) / (df + 0.5d)));
            var normalizedLength = _avgDocumentLength <= 0 ? 1d : chunk.TokenCount / (double)_avgDocumentLength;
            var numerator = tf * (k1 + 1d);
            var denominator = tf + (k1 * (1d - b + (b * normalizedLength)));

            score += idf * (numerator / denominator);
        }

        return (float)Math.Max(0d, score);
    }

    private static float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var length = Math.Min(left.Length, right.Length);
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < length; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            dot += leftValue * rightValue;
            leftNorm += leftValue * leftValue;
            rightNorm += rightValue * rightValue;
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0;
        }

        return (float)(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)));
    }

    private static string BuildRetrievalDebugText(string originalQuestion, string retrievalQuestion, RetrievalResult retrieval)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"原始问题: {originalQuestion}");
        if (!string.Equals(originalQuestion, retrievalQuestion, StringComparison.Ordinal))
        {
            builder.AppendLine($"检索问题: {retrievalQuestion}");
        }

        builder.AppendLine($"问题类型: {retrieval.QueryProfile.Intent}");
        if (retrieval.QueryProfile.FocusTerms.Count > 0)
        {
            builder.AppendLine($"焦点词: {string.Join(", ", retrieval.QueryProfile.FocusTerms)}");
        }
        if (!string.IsNullOrWhiteSpace(retrieval.QueryProfile.RequestedDocumentTitle))
        {
            builder.AppendLine($"指定文档: {retrieval.QueryProfile.RequestedDocumentTitle}");
            if (retrieval.TargetFilePaths.Count > 0)
            {
                builder.AppendLine($"命中文档文件: {string.Join(", ", retrieval.TargetFilePaths)}");
            }
        }

        builder.AppendLine($"稀疏预筛候选: {retrieval.SparseCandidateCount} 条");
        builder.AppendLine($"进入语义计算: {retrieval.SemanticCandidateCount} 条");
        builder.AppendLine($"是否使用稀疏预筛: {(retrieval.UsedSparsePrefilter ? "是" : "否（回退全量语义）")}");
        builder.AppendLine($"学习式重排: {(retrieval.UsedLearnedReranker ? "BGE-Reranker" : "未使用")} ({retrieval.LearnedRerankerStatus})");
        builder.AppendLine($"EvidencePlan: scope={retrieval.EvidencePlan.Scope}, mode={retrieval.EvidencePlan.Mode}, source={retrieval.EvidencePlan.SourcePreference}, sufficiency={retrieval.EvidencePlan.SufficiencyRule}");
        if (retrieval.EvidencePlan.RequiredSlots.Count > 0)
        {
            builder.AppendLine($"EvidencePlan slots: {string.Join(", ", retrieval.EvidencePlan.RequiredSlots.Select(slot => slot.Name))}");
        }
        builder.AppendLine($"EvidenceSet: sufficient={(retrieval.EvidenceSet.IsSufficient ? "yes" : "no")}, coveredSlots={(retrieval.EvidenceSet.CoveredSlots.Count == 0 ? "none" : string.Join(", ", retrieval.EvidenceSet.CoveredSlots))}");
        if (!string.IsNullOrWhiteSpace(retrieval.EvidenceSet.UnknownReason))
        {
            builder.AppendLine($"EvidenceSet unknown: {retrieval.EvidenceSet.UnknownReason}");
        }

        if (retrieval.RankedChunks.Count == 0)
        {
            builder.AppendLine("命中: 0 条");
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine($"主命中: {retrieval.RankedChunks.Count} 条");
        for (var index = 0; index < retrieval.RankedChunks.Count; index++)
        {
            var item = retrieval.RankedChunks[index];
            var snippet = item.Chunk.Text.Replace("\n", " ", StringComparison.Ordinal);
            if (snippet.Length > 120)
            {
                snippet = snippet[..120] + "...";
            }

            builder.AppendLine($"[{index + 1}] score={item.Score:F3} (semantic={item.SemanticScore:F3}, bm25={item.Bm25Score:F3}, keyword={item.KeywordScore:F3}, title={item.TitleScore:F3}, jsonStructure={item.JsonStructureScore:F3}, coverage={item.CoverageScore:F3}, neighbor={item.NeighborScore:F3}, jsonBranch={item.JsonBranchScore:F3}, docTarget={item.RequestedDocumentScore:F3}, directHit={item.HasDirectKeywordHit}) | {BuildStableCitation(item.Chunk)}");
            builder.AppendLine($"    {snippet}");
        }

        if (retrieval.ContextChunks.Count > 0)
        {
            builder.AppendLine($"上下文窗口: {retrieval.ContextChunks.Count} 条");
        }

        return builder.ToString().TrimEnd();
    }

    private static string NormalizeQuestionForRetrieval(string question, IReadOnlyList<ChatTurn> history)
    {
        var normalized = question.Trim();
        normalized = LeadingGreetingRegex.Replace(normalized, string.Empty);
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        normalized = StripQuestionBoilerplate(normalized);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (ShouldAugmentWithPreviousQuestion(normalized))
        {
            var previousQuestion = history
                .Where(turn => turn.IsUser)
                .Select(turn => turn.Content.Trim())
                .Where(content => !string.IsNullOrWhiteSpace(content))
                .Where(content => !string.Equals(content, question.Trim(), StringComparison.Ordinal))
                .Reverse()
                .FirstOrDefault(content => ExtractQueryTokens(content).Count > 0);

            if (!string.IsNullOrWhiteSpace(previousQuestion))
            {
                return $"{previousQuestion}；补充要求：{normalized}";
            }
        }

        return normalized.Trim();
    }

    private static string StripQuestionBoilerplate(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        var patterns = new[]
        {
            @"^\s*(?:对于|关于)\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))\s*[,，:：]?\s*",
            @"^\s*在\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料)|文中|文档里)\s*[,，:：]?\s*",
            @"^\s*针对\s*[^，。,.\n]{2,80}?(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))\s*[,，:：]?\s*"
        };

        foreach (var pattern in patterns)
        {
            normalized = Regex.Replace(normalized, pattern, string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        return BoundaryNoiseRegex.Replace(normalized, string.Empty).Trim();
    }

    private string PrepareTextForEmbedding(string text)
    {
        if (_embeddingWeights is null)
        {
            return text;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var tokens = _embeddingWeights.Tokenize(normalized, add_bos: true, special: false, encoding: Encoding.UTF8).ToArray();
        if (tokens.Length <= MaxEmbeddingTokens)
        {
            return normalized;
        }

        var shortened = normalized;
        while (shortened.Length > 32)
        {
            shortened = ShortenForEmbedding(shortened);
            tokens = _embeddingWeights.Tokenize(shortened, add_bos: true, special: false, encoding: Encoding.UTF8).ToArray();
            if (tokens.Length <= MaxEmbeddingTokens)
            {
                return shortened;
            }
        }

        return normalized[..Math.Min(normalized.Length, 256)];
    }

    private static string ShortenForEmbedding(string text)
    {
        var paragraphs = SplitParagraphs(text).ToArray();
        if (paragraphs.Length >= 3)
        {
            var keepCount = Math.Max(1, paragraphs.Length - 1);
            return string.Join("\n\n", paragraphs.Take(keepCount));
        }

        var sentences = SentenceSplitRegex.Split(text)
            .Select(sentence => sentence.Trim())
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToArray();

        if (sentences.Length >= 3)
        {
            var keepCount = Math.Max(1, sentences.Length - 1);
            return string.Join(' ', sentences.Take(keepCount));
        }

        return text[..Math.Max(32, text.Length * 3 / 4)].Trim();
    }

    private static QueryProfile BuildQueryProfile(string question)
    {
        var requestedDocumentTitle = ExtractRequestedDocumentTitle(question);
        var focusTerms = BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(IsMeaningfulFocusTerm)
            .OrderByDescending(token => token.Length)
            .Take(8)
            .ToArray();

        var intent = DetectIntent(question);
        var wantsDetailedAnswer = WantsDetailedAnswer(question);
        return new QueryProfile(question, focusTerms, intent, wantsDetailedAnswer, requestedDocumentTitle, AvoidsEnglishMetadata(question));
    }

    private static bool AvoidsEnglishMetadata(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var hasNegativeDirective = question.Contains("不要", StringComparison.Ordinal)
            || question.Contains("别", StringComparison.Ordinal)
            || question.Contains("无需", StringComparison.Ordinal)
            || question.Contains("不用", StringComparison.Ordinal);
        if (!hasNegativeDirective)
        {
            return false;
        }

        return EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase))
            || question.Contains("英文摘要", StringComparison.Ordinal)
            || question.Contains("英文关键词", StringComparison.Ordinal)
            || question.Contains("英文", StringComparison.OrdinalIgnoreCase)
            || question.Contains("english", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMeaningfulFocusTerm(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (FocusStopWords.Contains(token))
        {
            return false;
        }

        if (IsSupplementalRequestToken(token))
        {
            return false;
        }

        var hasAsciiLetterOrDigit = token.Any(character => char.IsAsciiLetterOrDigit(character));
        if (!hasAsciiLetterOrDigit)
        {
            if (FocusStopWords.Any(stopWord => token.Contains(stopWord, StringComparison.Ordinal)))
            {
                return false;
            }

            if (SupplementalRequestSignals.Any(signal =>
                    token.Contains(signal, StringComparison.Ordinal)
                    || signal.Contains(token, StringComparison.Ordinal)))
            {
                return false;
            }

            if (token.Length <= 2)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSupplementalRequestToken(string token)
    {
        return SupplementalRequestSignals.Any(signal =>
            token.Contains(signal, StringComparison.Ordinal)
            || signal.Contains(token, StringComparison.Ordinal));
    }

    private static string BuildFocusTermSourceText(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var markerIndex = question.IndexOf("；补充要求：", StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return question;
        }

        return StripRetrievalDirectives(question[..markerIndex].Trim());
    }

    private static IReadOnlyList<string> BuildRetrievalQueryTokens(string question, string? requestedDocumentTitle = null)
    {
        var focusSource = BuildFocusTermSourceText(question);
        var baseQuestion = string.IsNullOrWhiteSpace(focusSource) ? question : focusSource;
        baseQuestion = RemoveRequestedDocumentMentions(baseQuestion, requestedDocumentTitle);
        baseQuestion = StripRetrievalDirectives(baseQuestion);
        var tokens = new HashSet<string>(ExtractQueryTokens(baseQuestion), StringComparer.Ordinal);
        var subject = ExtractQuestionSubject(baseQuestion);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            tokens.Add(subject.ToLowerInvariant());
            foreach (var token in ExtractQueryTokens(subject))
            {
                tokens.Add(token);
            }
        }

        var expansionSource = string.IsNullOrWhiteSpace(question) ? baseQuestion : RemoveRequestedDocumentMentions(question, requestedDocumentTitle);
        expansionSource = StripRetrievalDirectives(expansionSource);
        var lowerQuestion = expansionSource.ToLowerInvariant();
        var intent = DetectIntent(expansionSource);

        foreach (var expansion in ExpandRetrievalTerms(expansionSource, lowerQuestion, intent))
        {
            if (!string.IsNullOrWhiteSpace(expansion))
            {
                tokens.Add(expansion.ToLowerInvariant());
            }
        }

        return tokens.ToArray();
    }

    private static string RemoveRequestedDocumentMentions(string question, string? requestedDocumentTitle)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        if (string.IsNullOrWhiteSpace(requestedDocumentTitle))
        {
            return normalized;
        }

        var title = requestedDocumentTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return normalized;
        }

        var escapedTitle = Regex.Escape(title);
        normalized = Regex.Replace(normalized, $"《\\s*{escapedTitle}\\s*》", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, $"[“\"]\\s*{escapedTitle}\\s*[”\"]", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, escapedTitle, string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料))", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    private static string StripRetrievalDirectives(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = question.Trim();
        normalized = Regex.Replace(
            normalized,
            @"(?:[，,；;。]\s*|\s+)(?:不要|别|无需|不用)(?:再)?[^，,；;。!?！？]*",
            string.Empty,
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");
        normalized = BoundaryNoiseRegex.Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    private IReadOnlyList<string> ResolveRequestedDocumentFilePaths(string? requestedDocumentTitle)
    {
        if (string.IsNullOrWhiteSpace(requestedDocumentTitle) || _chunks.Count == 0)
        {
            return [];
        }

        var normalizedTarget = NormalizeLookupText(requestedDocumentTitle);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return [];
        }

        var matches = _chunks
            .GroupBy(chunk => chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var titles = group
                    .Select(chunk => chunk.DocumentTitle)
                    .Append(Path.GetFileNameWithoutExtension(group.Key))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var score = titles
                    .Select(title => ComputeRequestedDocumentMatchScore(normalizedTarget, title))
                    .Append(ComputeRequestedDocumentMatchScore(normalizedTarget, group.Key))
                    .Max();

                return new { FilePath = group.Key, Score = score };
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            return [];
        }

        var bestScore = matches[0].Score;
        return matches
            .Where(item => item.Score == bestScore)
            .Select(item => item.FilePath)
            .ToArray();
    }

    private static string? ExtractRequestedDocumentTitle(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var guillemetMatch = Regex.Match(question, "《(?<title>[^》]{2,120})》");
        if (guillemetMatch.Success)
        {
            return guillemetMatch.Groups["title"].Value.Trim();
        }

        var quoteMatch = Regex.Match(question, "[“\"](?<title>[^”\"]{2,120})[”\"]");
        if (quoteMatch.Success)
        {
            return quoteMatch.Groups["title"].Value.Trim();
        }

        var tailMatch = Regex.Match(
            question,
            @"(?<title>[\p{IsCJKUnifiedIdeographs}A-Za-z0-9_\-：:（）()\s]{4,120}?)(?:这篇(?:论文|文章|文档|研究)|这份(?:文档|材料)|文档里|文中)",
            RegexOptions.IgnoreCase);
        return tailMatch.Success ? tailMatch.Groups["title"].Value.Trim() : null;
    }

    private static int ComputeRequestedDocumentMatchScore(string normalizedTarget, string? candidate)
    {
        var normalizedCandidate = NormalizeLookupText(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        if (string.Equals(normalizedTarget, normalizedCandidate, StringComparison.Ordinal))
        {
            return 6;
        }

        if (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal) || normalizedTarget.Contains(normalizedCandidate, StringComparison.Ordinal))
        {
            return 4;
        }

        var sharedLength = ExtractQueryTokens(normalizedTarget)
            .Intersect(ExtractQueryTokens(normalizedCandidate), StringComparer.Ordinal)
            .Sum(token => token.Length);

        return sharedLength >= 8 ? 2 : 0;
    }

    private static string NormalizeLookupText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || (character >= '\u4e00' && character <= '\u9fff'))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ApplyRequestedFileFormatPreference(string question, string intent, IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count <= 1)
        {
            return filePaths;
        }

        if (question.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonMatches = filePaths.Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (jsonMatches.Length > 0)
            {
                return jsonMatches;
            }
        }

        if (ShouldPreferStructuredJsonForIntent(intent))
        {
            var preferred = PreferJsonWhenSameDocumentHasMarkdown(filePaths);
            if (preferred.Count > 0 && preferred.Count < filePaths.Count)
            {
                return preferred;
            }
        }

        return filePaths;
    }

    private static bool ShouldPreferStructuredJsonForIntent(string intent)
    {
        return intent is "summary" or "composition" or "usage" or "module_list" or "module_implementation" or "explain" or "procedure";
    }

    private static IReadOnlyList<string> PreferJsonWhenSameDocumentHasMarkdown(IReadOnlyList<string> filePaths)
    {
        var grouped = filePaths
            .GroupBy(path => NormalizeLookupText(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selected = new List<string>();
        var changed = false;
        foreach (var group in grouped)
        {
            var paths = group.ToArray();
            var jsonPaths = paths.Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
            var hasMarkdown = paths.Any(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            if (jsonPaths.Length > 0 && hasMarkdown)
            {
                selected.AddRange(jsonPaths);
                changed = true;
                continue;
            }

            selected.AddRange(paths);
        }

        return changed ? selected : filePaths;
    }

    private static IEnumerable<string> ExpandRetrievalTerms(string question, string lowerQuestion, string intent)
    {
        var expansions = new HashSet<string>(StringComparer.Ordinal);

        if (intent == "summary" || question.Contains("主要写了什么", StringComparison.Ordinal) || question.Contains("主要内容", StringComparison.Ordinal))
        {
            expansions.Add("overview");
            expansions.Add("summary");
            expansions.Add("abstract");
            expansions.Add("摘要");
            expansions.Add("概述");
            expansions.Add("引言");
            expansions.Add("结论");
            expansions.Add("主要内容");
        }

        if (question.Contains("创新点", StringComparison.Ordinal)
            || question.Contains("创新", StringComparison.Ordinal)
            || question.Contains("贡献", StringComparison.Ordinal))
        {
            expansions.Add("创新点");
            expansions.Add("创新");
            expansions.Add("贡献");
            expansions.Add("提出");
            expansions.Add("改进");
            expansions.Add("优势");
        }

        if (question.Contains("局限", StringComparison.Ordinal)
            || question.Contains("不足", StringComparison.Ordinal)
            || question.Contains("挑战", StringComparison.Ordinal)
            || question.Contains("问题", StringComparison.Ordinal)
            || question.Contains("启示", StringComparison.Ordinal)
            || question.Contains("未来", StringComparison.Ordinal)
            || question.Contains("反思", StringComparison.Ordinal))
        {
            expansions.Add("局限");
            expansions.Add("不足");
            expansions.Add("挑战");
            expansions.Add("问题");
            expansions.Add("未来");
            expansions.Add("改进");
            expansions.Add("研究局限");
            expansions.Add("反思");
        }

        if (question.Contains("架构", StringComparison.Ordinal)
            || question.Contains("技术栈", StringComparison.Ordinal)
            || question.Contains("总体设计", StringComparison.Ordinal)
            || question.Contains("前后端", StringComparison.Ordinal))
        {
            expansions.Add("架构");
            expansions.Add("系统架构");
            expansions.Add("总体架构");
            expansions.Add("总体设计");
            expansions.Add("技术栈");
            expansions.Add("前后端分离");
            expansions.Add("后端");
            expansions.Add("前端");
            expansions.Add("数据库");
            expansions.Add("可视化");
            expansions.Add("模块");
            expansions.Add("接口");
        }

        if (question.Contains("硬件", StringComparison.Ordinal))
        {
            expansions.Add("硬件设计");
            expansions.Add("主控");
            expansions.Add("主控芯片");
            expansions.Add("传感器");
            expansions.Add("通信");
            expansions.Add("显示");
            expansions.Add("电源");
            expansions.Add("接口");
        }

        if (question.Contains("软件", StringComparison.Ordinal)
            || question.Contains("运行环境", StringComparison.Ordinal)
            || question.Contains("部署环境", StringComparison.Ordinal))
        {
            expansions.Add("软件设计");
            expansions.Add("运行环境");
            expansions.Add("部署环境");
            expansions.Add("依赖");
            expansions.Add("模块");
            expansions.Add("配置");
        }

        if (intent == "definition"
            || question.Contains("是什么", StringComparison.Ordinal)
            || question.Contains("指什么", StringComparison.Ordinal)
            || question.Contains("定义", StringComparison.Ordinal))
        {
            expansions.Add("定义");
            expansions.Add("是指");
            expansions.Add("是一种");
            expansions.Add("作为");
            expansions.Add("用于");
            expansions.Add("负责");
            expansions.Add("核心");
        }

        if (intent == "explain"
            || question.Contains("为什么", StringComparison.Ordinal)
            || question.Contains("原理", StringComparison.Ordinal)
            || question.Contains("机制", StringComparison.Ordinal))
        {
            expansions.Add("原因");
            expansions.Add("机制");
            expansions.Add("原理");
            expansions.Add("由于");
            expansions.Add("因为");
            expansions.Add("因此");
            expansions.Add("影响");
            expansions.Add("动态调节");
        }

        if (intent == "compare"
            || question.Contains("区别", StringComparison.Ordinal)
            || question.Contains("对比", StringComparison.Ordinal)
            || question.Contains("比较", StringComparison.Ordinal))
        {
            expansions.Add("区别");
            expansions.Add("差异");
            expansions.Add("不同");
            expansions.Add("对比");
            expansions.Add("相比");
            expansions.Add("阶段");
        }

        if (intent == "procedure")
        {
            expansions.Add("步骤");
            expansions.Add("流程");
            expansions.Add("策略");
            expansions.Add("机制");
            expansions.Add("算法");
            expansions.Add("反馈");
            expansions.Add("输入");
            expansions.Add("输出");
            expansions.Add("执行");
        }

        if (IsModuleQuestion(question))
        {
            expansions.Add("模块");
            expansions.Add("功能模块");
            expansions.Add("功能");
            expansions.Add("组成");
            expansions.Add("职责");
        }

        if (EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            expansions.Add("englishtitle");
            expansions.Add("englishabstract");
            expansions.Add("englishkeywords");
            expansions.Add("english");
        }

        if (intent == "module_implementation"
            || question.Contains("远程", StringComparison.Ordinal)
            || question.Contains("状态显示", StringComparison.Ordinal)
            || question.Contains("控制模块", StringComparison.Ordinal))
        {
            expansions.Add("接口");
            expansions.Add("协议");
            expansions.Add("通信");
            expansions.Add("链路");
            expansions.Add("上传");
            expansions.Add("下发");
            expansions.Add("显示");
            expansions.Add("控制");
        }

        if (intent == "usage"
            || question.Contains("作用", StringComparison.Ordinal)
            || question.Contains("用途", StringComparison.Ordinal)
            || question.Contains("功能", StringComparison.Ordinal))
        {
            expansions.Add("用于");
            expansions.Add("负责");
            expansions.Add("显示");
            expansions.Add("控制");
            expansions.Add("采集");
        }

        return expansions;
    }

    private static string DetectIntent(string question)
    {
        if (IsSummaryQuestion(question))
        {
            return "summary";
        }

        if (question.Contains("区别", StringComparison.Ordinal) || question.Contains("对比", StringComparison.Ordinal) || question.Contains("比较", StringComparison.Ordinal))
        {
            return "compare";
        }

        if (question.Contains("为什么", StringComparison.Ordinal) || question.Contains("原因", StringComparison.Ordinal) || question.Contains("原理", StringComparison.Ordinal) || question.Contains("机制", StringComparison.Ordinal))
        {
            return "explain";
        }

        if (EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            return "metadata";
        }

        if (IsModuleQuestion(question))
        {
            return "module_list";
        }

        if (IsModuleImplementationQuestion(question))
        {
            return "module_implementation";
        }

        if (IsCompositionQuestion(question))
        {
            return "composition";
        }

        if (IsProcedureQuestion(question))
        {
            return "procedure";
        }

        if (question.Contains("作用", StringComparison.Ordinal) || question.Contains("用途", StringComparison.Ordinal) || question.Contains("功能", StringComparison.Ordinal) || question.Contains("有什么用", StringComparison.Ordinal))
        {
            return "usage";
        }

        if (question.Contains("是什么", StringComparison.Ordinal) || question.Contains("指什么", StringComparison.Ordinal) || question.Contains("定义", StringComparison.Ordinal))
        {
            return "definition";
        }

        return "general";
    }

    private IReadOnlyList<DocumentChunk> SplitIntoChunks(string content, string sourceName, string relativePath)
    {
        var normalized = SanitizeMarkdownForIndex(content.Replace("\r\n", "\n", StringComparison.Ordinal));
        var chunks = new List<DocumentChunk>();

        var documentTitle = ExtractDocumentTitle(normalized, sourceName);
        var sectionTitle = documentTitle;
        var structurePath = documentTitle;
        var headingStack = new List<string>();
        var chunkIndex = 0;
        var buffer = new StringBuilder();
        var pendingOverlap = string.Empty;

        foreach (var paragraph in SplitParagraphs(normalized))
        {
            if (IsHeadingParagraph(paragraph))
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);
                pendingOverlap = string.Empty;
                sectionTitle = NormalizeMarkdownHeading(paragraph);
                structurePath = UpdateMarkdownHeadingPath(headingStack, paragraph, sectionTitle, documentTitle);
                buffer.AppendLine(paragraph.Trim());
                continue;
            }

            var trimmedParagraph = paragraph.Trim();
            if (buffer.Length > 0 && buffer.Length + trimmedParagraph.Length + 2 > _options.ChunkSize)
            {
                pendingOverlap = FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: true);
            }

            if (trimmedParagraph.Length > _options.ChunkSize)
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);
                pendingOverlap = string.Empty;
                foreach (var oversizedPart in SplitLargeParagraph(trimmedParagraph))
                {
                    chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, oversizedPart, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, "body"));
                }

                continue;
            }

            AppendPendingOverlap(buffer, pendingOverlap, trimmedParagraph.Length);
            pendingOverlap = string.Empty;

            if (buffer.Length > 0)
            {
                buffer.AppendLine();
            }

            buffer.Append(trimmedParagraph);
        }

        FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);

        return chunks;
    }

    private static string SanitizeMarkdownForIndex(string content)
    {
        var cleanedLines = new List<string>();
        foreach (var rawLine in content.Split('\n', StringSplitOptions.None))
        {
            var line = MarkdownImageRegex.Replace(rawLine, string.Empty);
            line = MarkdownLinkRegex.Replace(line, "$1").TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                cleanedLines.Add(string.Empty);
                continue;
            }

            var trimmed = line.Trim();
            if (HtmlTagRegex.IsMatch(trimmed) && HtmlTagRegex.Replace(trimmed, string.Empty).Trim().Length == 0)
            {
                continue;
            }

            line = HtmlTagRegex.Replace(line, string.Empty).TrimEnd();
            if (string.IsNullOrWhiteSpace(line)
                || IsMetadataLikeSentence(line)
                || IsReferenceLikeSentence(line)
                || IsTitleOrAuthorLikeLine(line))
            {
                continue;
            }

            cleanedLines.Add(line);
        }

        return string.Join('\n', cleanedLines);
    }

    private static string NormalizeMarkdownHeading(string paragraph)
    {
        return paragraph.Trim().TrimStart('#', ' ', '\t').Trim();
    }

    private static string UpdateMarkdownHeadingPath(List<string> headingStack, string paragraph, string sectionTitle, string documentTitle)
    {
        var level = GetMarkdownHeadingLevel(paragraph);
        if (level <= 0)
        {
            level = headingStack.Count == 0 ? 1 : Math.Min(headingStack.Count + 1, 6);
        }

        while (headingStack.Count >= level)
        {
            headingStack.RemoveAt(headingStack.Count - 1);
        }

        headingStack.Add(sectionTitle);
        return headingStack.Count == 0 ? documentTitle : string.Join(" > ", headingStack);
    }

    private static int GetMarkdownHeadingLevel(string paragraph)
    {
        var trimmed = paragraph.TrimStart();
        var count = 0;
        while (count < trimmed.Length && count < 6 && trimmed[count] == '#')
        {
            count++;
        }

        return count > 0 && count < trimmed.Length && char.IsWhiteSpace(trimmed[count]) ? count : 0;
    }

    private static string ExtractDocumentTitle(string content, string sourceName)
    {
        var firstHeading = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(IsHeadingParagraph);

        if (!string.IsNullOrWhiteSpace(firstHeading))
        {
            return firstHeading.TrimStart('#', ' ').Trim();
        }

        return Path.GetFileNameWithoutExtension(sourceName);
    }

    private static IEnumerable<string> SplitParagraphs(string content)
    {
        return content
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));
    }

    private static bool IsHeadingParagraph(string paragraph)
    {
        var trimmed = paragraph.Trim();
        return HeadingLineRegex.IsMatch(trimmed)
            || IsPlainTextSectionHeading(trimmed);
    }

    private static bool IsPlainTextSectionHeading(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 42)
        {
            return false;
        }

        if (text.Contains('。') || text.Contains('，') || text.Contains('.') || text.Contains(';') || text.Contains('；'))
        {
            return false;
        }

        if (IsMetadataLikeSentence(text) || IsReferenceLikeSentence(text) || IsTitleOrAuthorLikeLine(text))
        {
            return false;
        }

        if (text.StartsWith('|') || text.StartsWith('-') || text.StartsWith('*'))
        {
            return false;
        }

        var hasHeadingSignal = Regex.IsMatch(text, @"^\s*(?:第?[一二三四五六七八九十\d]+[章节条部分]?|[一二三四五六七八九十\d]+(?:\.\d+)*|附录|摘要|引言|结论|参考文献)(?:\s|、|：|:)")
            || CountSignals(text, "摘要", "引言", "绪论", "方法", "系统", "设计", "实现", "实验", "结果", "讨论", "结论", "模块", "架构", "流程") > 0;

        return hasHeadingSignal;
    }

    private string FlushChunkBuffer(
        List<DocumentChunk> chunks,
        StringBuilder buffer,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex,
        bool allowOverlapForNextChunk)
    {
        var text = buffer.ToString().Trim();
        buffer.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, text, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, "body"));
        return allowOverlapForNextChunk ? BuildChunkOverlapPrefix(text) : string.Empty;
    }

    private void AppendPendingOverlap(StringBuilder buffer, string pendingOverlap, int nextParagraphLength)
    {
        if (buffer.Length > 0 || string.IsNullOrWhiteSpace(pendingOverlap) || _options.ChunkOverlap <= 0)
        {
            return;
        }

        var maxAllowedLength = Math.Max(0, _options.ChunkSize - nextParagraphLength - 2);
        if (maxAllowedLength <= 0)
        {
            return;
        }

        var overlap = pendingOverlap.Trim();
        if (overlap.Length > maxAllowedLength)
        {
            overlap = overlap[^maxAllowedLength..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(overlap))
        {
            buffer.Append(overlap);
        }
    }

    private string BuildChunkOverlapPrefix(string chunkText)
    {
        if (_options.ChunkOverlap <= 0 || string.IsNullOrWhiteSpace(chunkText))
        {
            return string.Empty;
        }

        var normalized = chunkText.Trim();
        var overlapLength = Math.Min(_options.ChunkOverlap, normalized.Length);
        if (overlapLength <= 0)
        {
            return string.Empty;
        }

        return normalized[^overlapLength..].Trim();
    }

    private IEnumerable<string> SplitLargeParagraph(string paragraph)
    {
        var normalized = paragraph.Trim();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(_options.ChunkSize, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var lastBreak = normalized.LastIndexOfAny(['。', '！', '？', '\n'], end - 1, length);
                if (lastBreak > start + (_options.ChunkSize / 2))
                {
                    end = lastBreak + 1;
                }
            }

            var text = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - _options.ChunkOverlap);
        }
    }

    private static IReadOnlyList<string> ExtractQueryTokens(string text)
    {
        return EnumerateTokens(text)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateTokens(string text)
    {
        foreach (Match match in QueryTokenRegex.Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length < 2)
            {
                continue;
            }

            yield return token;
            if (ContainsCjk(token) && token.Length > 3)
            {
                for (var index = 0; index < token.Length - 1; index++)
                {
                    yield return token.Substring(index, 2);
                }
            }
        }
    }

    private static (float KeywordScore, bool HasDirectKeywordHit) ComputeKeywordScore(IReadOnlyList<string> queryTokens, string chunkText)
    {
        if (queryTokens.Count == 0)
        {
            return (0f, false);
        }

        var normalizedChunk = chunkText.ToLowerInvariant();
        var totalWeight = 0f;
        var hitWeight = 0f;
        var hasDirectHit = false;

        foreach (var token in queryTokens)
        {
            var tokenWeight = token.Length >= 4 ? 2f : 1f;
            totalWeight += tokenWeight;
            if (!normalizedChunk.Contains(token, StringComparison.Ordinal))
            {
                continue;
            }

            hitWeight += tokenWeight;
            if (!ContainsCjk(token) || token.Length >= 4)
            {
                hasDirectHit = true;
            }
        }

        return totalWeight <= 0f ? (0f, hasDirectHit) : (hitWeight / totalWeight, hasDirectHit);
    }

    private static float ComputeTitleScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0)
        {
            return 0f;
        }

        var titleCorpus = $"{chunk.DocumentTitle} {chunk.SectionTitle} {chunk.StructurePath}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(titleCorpus))
        {
            return 0f;
        }

        var matched = focusTerms.Count(term => titleCorpus.Contains(term, StringComparison.Ordinal));
        var baseScore = matched <= 0 ? 0f : matched / (float)focusTerms.Count;
        return baseScore * GetContentKindTitleMultiplier(chunk.ContentKind);
    }

    private static float ComputeJsonStructureScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0 || !IsJsonChunk(chunk))
        {
            return 0f;
        }

        var segments = GetNormalizedStructureSegments(chunk.StructurePath);
        if (segments.Length == 0)
        {
            return 0f;
        }

        float matchedWeight = 0f;
        foreach (var term in focusTerms)
        {
            var bestMatch = 0f;
            for (var index = 0; index < segments.Length; index++)
            {
                if (!segments[index].Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var segmentWeight = index == segments.Length - 1 ? 1f : 0.55f;
                bestMatch = Math.Max(bestMatch, segmentWeight);
            }

            if (chunk.SectionTitle.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                bestMatch = Math.Max(bestMatch, 0.8f);
            }

            matchedWeight += bestMatch;
        }

        var normalized = Math.Clamp(matchedWeight / focusTerms.Count, 0f, 1f);
        return normalized * GetJsonStructureKindMultiplier(chunk.ContentKind);
    }

    private static float GetJsonStructureKindMultiplier(string contentKind)
    {
        return contentKind switch
        {
            "summary" => 1.1f,
            "body" or "" => 1f,
            "keyword" => 0.9f,
            "title" => 0.7f,
            "metadata" => 0.15f,
            _ => 0.85f
        };
    }

    private static float ComputeNoisePenalty(DocumentChunk chunk)
    {
        var text = chunk.Text.Trim();
        var lower = text.ToLowerInvariant();
        var penalty = 0f;

        if (MetadataLineRegex.IsMatch(text))
        {
            penalty += 0.14f;
        }

        if (text.StartsWith("#", StringComparison.Ordinal) && text.Length < 120)
        {
            penalty += 0.12f;
        }

        if (text.StartsWith("参考文献", StringComparison.Ordinal)
            || text.StartsWith("# 参考文献", StringComparison.Ordinal)
            || IsReferenceLikeSentence(text))
        {
            penalty += 0.3f;
        }

        if (text.Contains("关键词", StringComparison.Ordinal) || lower.Contains("key words", StringComparison.Ordinal))
        {
            penalty += 0.18f;
        }

        if (HtmlTagRegex.IsMatch(text) || MarkdownImageRegex.IsMatch(text))
        {
            penalty += 0.18f;
        }

        if (text.Contains("中图分类号", StringComparison.Ordinal)
            || text.Contains("文献标识码", StringComparison.Ordinal)
            || lower.Contains("doi", StringComparison.Ordinal))
        {
            penalty += 0.18f;
        }

        if (lower.StartsWith("abstract:", StringComparison.Ordinal))
        {
            penalty += 0.08f;
        }

        if (chunk.ContentKind == "metadata")
        {
            penalty += 0.28f;
            if (text.Length < 120)
            {
                penalty += 0.08f;
            }
        }

        if (chunk.ContentKind == "keyword")
        {
            penalty -= 0.04f;
        }

        if (chunk.ContentKind == "summary")
        {
            penalty -= 0.03f;
        }

        return Math.Min(0.45f, penalty);
    }

    private static float GetContentKindTitleMultiplier(string contentKind)
    {
        return contentKind switch
        {
            "title" => 1.35f,
            "summary" => 1.15f,
            "keyword" => 1.2f,
            "metadata" => 0.55f,
            _ => 1f
        };
    }

    private ScoredChunk[] RerankCandidates(IReadOnlyList<ScoredChunk> candidates, QueryProfile queryProfile, EvidencePlan evidencePlan, int count)
    {
        var ranked = candidates
            .Select(candidate =>
            {
                var coverageScore = ComputeCoverageScore(queryProfile.FocusTerms, candidate.Chunk);
                var neighborScore = ComputeNeighborSupportScore(queryProfile.FocusTerms, candidate.Chunk);
                var jsonBranchScore = ComputeJsonBranchSupportScore(queryProfile.FocusTerms, candidate.Chunk);
                var intentBoost = ComputeIntentBoost(queryProfile.Intent, candidate.Chunk.Text);
                var chunkKindBoost = ComputeQueryAwareChunkKindBoost(queryProfile, candidate.Chunk);
                var evidenceBoost = ComputeEvidencePolicyBoost(evidencePlan, candidate.Chunk);
                var finalScore = candidate.Score
                    + (coverageScore * _options.CoverageWeight)
                    + (neighborScore * _options.NeighborWeight)
                    + (jsonBranchScore * _options.JsonBranchWeight)
                    + intentBoost
                    + chunkKindBoost
                    + evidenceBoost;

                return candidate with
                {
                    Score = finalScore,
                    CoverageScore = coverageScore,
                    NeighborScore = neighborScore,
                    JsonBranchScore = jsonBranchScore
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .Take(count)
            .ToArray();

        return ranked;
    }

    private static float ComputeCoverageScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0)
        {
            return 0f;
        }

        var normalized = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        var matched = focusTerms.Count(term => normalized.Contains(term, StringComparison.Ordinal));
        return matched <= 0 ? 0f : matched / (float)focusTerms.Count;
    }

    private float ComputeNeighborSupportScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0)
        {
            return 0f;
        }

        var neighbors = _indexedChunks
            .Where(item => string.Equals(item.Chunk.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Where(item => Math.Abs(item.Chunk.Index - chunk.Index) <= 1 && item.Chunk.Index != chunk.Index)
            .Select(item => BuildChunkRetrievalText(item.Chunk).ToLowerInvariant())
            .ToArray();

        if (neighbors.Length == 0)
        {
            return 0f;
        }

        var supported = focusTerms.Count(term => neighbors.Any(text => text.Contains(term, StringComparison.Ordinal)));
        return supported <= 0 ? 0f : supported / (float)focusTerms.Count;
    }

    private float ComputeJsonBranchSupportScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0 || !IsJsonChunk(chunk) || string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            return 0f;
        }

        var branchTexts = _indexedChunks
            .Where(item => string.Equals(item.Chunk.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Chunk.Index != chunk.Index)
            .Where(item => AreJsonChunksStructurallyRelated(chunk, item.Chunk))
            .Select(item => BuildChunkRetrievalText(item.Chunk).ToLowerInvariant())
            .ToArray();

        if (branchTexts.Length == 0)
        {
            return 0f;
        }

        var supported = focusTerms.Count(term => branchTexts.Any(text => text.Contains(term, StringComparison.Ordinal)));
        return supported <= 0 ? 0f : supported / (float)focusTerms.Count;
    }

    private static float ComputeIntentBoost(string intent, string chunkText)
    {
        var signals = intent switch
        {
            "module_list" => ModuleImplementationSignals,
            "module_implementation" => ModuleImplementationSignals,
            "composition" => CompositionSignals,
            "compare" => CompareSignals,
            "definition" => DefinitionSignals,
            "usage" => UsageSignals,
            "procedure" => ProcedureSignals,
            "explain" => ExplanationSignals,
            _ => Array.Empty<string>()
        };

        if (signals.Length == 0)
        {
            return 0f;
        }

        var matched = signals.Count(signal => chunkText.Contains(signal, StringComparison.Ordinal));
        return matched switch
        {
            <= 0 => 0f,
            1 => 0.02f,
            2 => 0.04f,
            _ => 0.06f
        };
    }

    private static float ComputeQueryAwareChunkKindBoost(QueryProfile queryProfile, DocumentChunk chunk)
    {
        var boost = 0f;

        if (queryProfile.AvoidsEnglishMetadata && IsEnglishFieldChunk(chunk))
        {
            boost -= 0.35f;
        }

        if (queryProfile.Intent is "procedure" or "module_implementation")
        {
            boost += chunk.ContentKind switch
            {
                "body" or "" => 0.04f,
                "summary" => -0.03f,
                "keyword" => -0.24f,
                "title" => -0.14f,
                "metadata" => -0.30f,
                _ => 0f
            };

            if (IsNarrowJsonExampleChunk(chunk))
            {
                boost -= 0.28f;
            }
        }

        if (queryProfile.Intent is "composition" or "compare" or "definition" or "explain")
        {
            boost += chunk.ContentKind switch
            {
                "body" or "" => 0.05f,
                "summary" => 0.02f,
                "keyword" => -0.18f,
                "title" => -0.10f,
                "metadata" => -0.26f,
                _ => 0f
            };
        }

        if (queryProfile.Intent == "procedure")
        {
            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            if (ContainsAny(structureText, "阶段", "步骤", "流程", "策略", "机制", "方法", "控制", "调节"))
            {
                boost += 0.10f;
            }

            if (ContainsAny(chunk.Text, "首先", "然后", "最后", "通过", "采用", "根据", "动态", "实时", "反馈"))
            {
                boost += 0.08f;
            }
        }

        if (queryProfile.Intent == "explain")
        {
            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            if (structureText.Contains("机制", StringComparison.Ordinal)
                || structureText.Contains("原理", StringComparison.Ordinal)
                || structureText.Contains("原因", StringComparison.Ordinal)
                || structureText.Contains("策略", StringComparison.Ordinal))
            {
                boost += 0.12f;
            }

            if (ContainsAny(chunk.Text, "由于", "因此", "因为", "导致", "影响", "为了", "从而", "所以"))
            {
                boost += 0.10f;
            }
        }

        if (queryProfile.Intent == "compare")
        {
            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            if (structureText.Contains("阶段", StringComparison.Ordinal)
                || structureText.Contains("对比", StringComparison.Ordinal)
                || structureText.Contains("比较", StringComparison.Ordinal)
                || structureText.Contains("区别", StringComparison.Ordinal))
            {
                boost += 0.12f;
            }

            if (ContainsAny(chunk.Text, "不同", "区别", "相比", "一方面", "另一方面", "而", "则"))
            {
                boost += 0.08f;
            }
        }

        if (queryProfile.Intent == "definition")
        {
            if (ContainsAny(chunk.Text, "作为", "是一种", "是指", "负责", "用于", "核心", "表示", "定义"))
            {
                boost += 0.10f;
            }
        }

        if (queryProfile.Intent == "composition")
        {
            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            if (ContainsAny(structureText, "层", "模块", "环境", "配置", "硬件", "软件", "接口", "架构", "技术栈", "前端", "后端", "可视化"))
            {
                boost += 0.08f;
            }

            var entityHits = CountGenericAnswerEntities($"{structureText} {chunk.Text}");
            if (entityHits > 0)
            {
                boost += Math.Min(0.20f, entityHits * 0.035f);
            }
        }

        if (queryProfile.Intent == "module_list")
        {
            var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
            var moduleSignals = CountSignals(source, "功能模块", "模块", "子系统", "功能", "组成", "包括", "包含", "职责");
            boost += Math.Min(0.30f, moduleSignals * 0.06f);
            if (ContainsAny(source, "集成了", "包括", "包含", "主要功能", "模块"))
            {
                boost += 0.08f;
            }
        }

        return boost;
    }

    private static bool IsNarrowJsonExampleChunk(DocumentChunk chunk)
    {
        if (!IsJsonChunk(chunk))
        {
            return false;
        }

        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        if (structureText.Contains("example", StringComparison.OrdinalIgnoreCase)
            || structureText.Contains("示例", StringComparison.Ordinal)
            || structureText.Contains("第", StringComparison.Ordinal) && structureText.Contains("项", StringComparison.Ordinal))
        {
            var lineCount = chunk.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
            return lineCount <= 4 || chunk.Text.Length <= 180;
        }

        return false;
    }

    private IReadOnlyList<DocumentChunk> BuildContextWindow(IReadOnlyList<ScoredChunk> rankedChunks, QueryProfile queryProfile, IReadOnlyList<string> targetFilePaths)
    {
        if (rankedChunks.Count == 0)
        {
            return [];
        }

        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var selected = new Dictionary<string, DocumentChunk>(StringComparer.Ordinal);
        var seedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ranked in rankedChunks)
        {
            if (targetFilePathSet is not null && !targetFilePathSet.Contains(ranked.Chunk.FilePath))
            {
                continue;
            }

            var start = Math.Max(0, ranked.Chunk.Index - _options.ContextWindowRadius);
            var end = ranked.Chunk.Index + _options.ContextWindowRadius;

            foreach (var indexed in _indexedChunks
                         .Where(item => string.Equals(item.Chunk.FilePath, ranked.Chunk.FilePath, StringComparison.OrdinalIgnoreCase))
                         .Where(item => item.Chunk.Index >= start && item.Chunk.Index <= end))
            {
                var key = $"{indexed.Chunk.FilePath}#{indexed.Chunk.Index}";
                if (!selected.ContainsKey(key))
                {
                    selected.Add(key, indexed.Chunk);
                }
            }

            seedKeys.Add($"{ranked.Chunk.FilePath}#{ranked.Chunk.Index}");
            AddJsonBranchContext(selected, ranked.Chunk, queryProfile.FocusTerms, targetFilePathSet);
            AddSectionRelatedContext(selected, ranked.Chunk, queryProfile, targetFilePathSet);
            if (queryProfile.Intent == "procedure")
            {
                AddJsonProcedureChapterContext(selected, ranked.Chunk, queryProfile.FocusTerms, targetFilePathSet);
            }
        }

        var maxContextChunks = Math.Clamp(Math.Max(6, rankedChunks.Count * (queryProfile.WantsDetailedAnswer ? 3 : 2)), 6, queryProfile.WantsDetailedAnswer ? 14 : 10);
        return selected.Values
            .Where(chunk => ShouldKeepChunkForAnswer(queryProfile, chunk))
            .Select(chunk => new
            {
                Chunk = chunk,
                IsSeed = seedKeys.Contains($"{chunk.FilePath}#{chunk.Index}"),
                Relevance = ComputeCoverageScore(queryProfile.FocusTerms, chunk)
                    + ComputeTitleScore(queryProfile.FocusTerms, chunk)
                    + ComputeJsonStructureScore(queryProfile.FocusTerms, chunk)
                    + (chunk.ContentKind == "summary" ? 0.15f : 0f)
            })
            .OrderByDescending(item => item.IsSeed)
            .ThenByDescending(item => item.Relevance)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .Take(maxContextChunks)
            .Select(item => item.Chunk)
            .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Index)
            .ToArray();
    }

    private EvidencePlan BuildEvidencePlan(QueryProfile queryProfile, IReadOnlyList<string> targetFilePaths)
    {
        var mode = MapEvidenceMode(queryProfile.Intent);
        if (IsAbsenceCheckQuestion(queryProfile.OriginalQuestion))
        {
            mode = EvidenceMode.AbsenceCheck;
        }

        var allowed = mode switch
        {
            EvidenceMode.Metadata => new[] { "metadata", "title", "summary", "keyword", "body", "" },
            EvidenceMode.Summary => new[] { "summary", "body", "title", "" },
            EvidenceMode.Composition or EvidenceMode.ModuleList or EvidenceMode.Implementation or EvidenceMode.Definition or EvidenceMode.Compare or EvidenceMode.Explain or EvidenceMode.Usage => new[] { "body", "summary", "" },
            _ => new[] { "body", "summary", "keyword", "title", "" }
        };
        var disallowed = mode == EvidenceMode.Metadata
            ? Array.Empty<string>()
            : new[] { "metadata" };
        if (IsChineseQuestion(queryProfile.OriginalQuestion) && mode != EvidenceMode.Metadata)
        {
            disallowed = disallowed.Append("english").ToArray();
        }

        var sourcePreference = ShouldPreferStructuredJsonForIntent(queryProfile.Intent)
            ? "structured-json"
            : "balanced";
        var scope = targetFilePaths.Count > 0
            ? (string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) ? "active-document" : "explicit-document")
            : "corpus-wide";
        return new EvidencePlan(
            scope,
            mode,
            BuildEvidenceSlots(queryProfile, mode),
            BuildPreferredSectionSignals(mode, queryProfile.OriginalQuestion),
            allowed,
            disallowed,
            sourcePreference,
            "group-rerank",
            BuildSufficiencyRule(mode),
            mode == EvidenceMode.AbsenceCheck ? "unknown-if-no-topic-hit-in-scope" : "unknown-if-insufficient",
            queryProfile);
    }

    private EvidenceSet BuildEvidenceSet(
        IReadOnlyList<ScoredChunk> rankedChunks,
        QueryProfile queryProfile,
        EvidencePlan evidencePlan,
        IReadOnlyList<string> targetFilePaths)
    {
        if (rankedChunks.Count == 0)
        {
            return new EvidenceSet([], [], false, "我不知道（当前文档未覆盖）。");
        }

        var candidateChunks = BuildContextWindow(rankedChunks, queryProfile, targetFilePaths)
            .Where(chunk => IsChunkAllowedByEvidencePlan(evidencePlan, chunk))
            .ToArray();
        if (candidateChunks.Length == 0)
        {
            candidateChunks = rankedChunks
                .Select(item => item.Chunk)
                .Where(chunk => IsChunkAllowedByEvidencePlan(evidencePlan, chunk))
                .DistinctBy(BuildChunkKey, StringComparer.Ordinal)
                .ToArray();
        }

        if (evidencePlan.RequiredSlots.Count == 0)
        {
            var selected = candidateChunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = ComputeEvidencePolicyBoost(evidencePlan, chunk)
                        + ComputeCoverageScore(queryProfile.FocusTerms, chunk)
                        + ComputeTitleScore(queryProfile.FocusTerms, chunk)
                        + ComputeJsonStructureScore(queryProfile.FocusTerms, chunk)
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Chunk.Index)
                .Take(queryProfile.WantsDetailedAnswer ? 12 : 8)
                .Select(item => item.Chunk)
                .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Index)
                .ToArray();
            return BuildEvidenceSufficiencyResult(evidencePlan, selected, []);
        }

        var selectedBySlot = new List<(EvidenceSlot Slot, DocumentChunk Chunk)>();
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        var perSlotLimit = queryProfile.WantsDetailedAnswer ? 2 : 1;
        foreach (var slot in evidencePlan.RequiredSlots)
        {
            var slotChunks = candidateChunks
                .Where(chunk => ChunkMatchesEvidenceSlot(chunk, slot))
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = ScoreChunkForEvidenceSlot(chunk, slot, evidencePlan, queryProfile)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Chunk.Index)
                .Take(perSlotLimit)
                .ToArray();

            foreach (var item in slotChunks)
            {
                var key = BuildChunkKey(item.Chunk);
                if (usedKeys.Add(key))
                {
                    selectedBySlot.Add((slot, item.Chunk));
                }
            }
        }

        foreach (var chunk in candidateChunks)
        {
            if (usedKeys.Count >= (queryProfile.WantsDetailedAnswer ? 14 : 10))
            {
                break;
            }

            var key = BuildChunkKey(chunk);
            if (usedKeys.Add(key))
            {
                selectedBySlot.Add((new EvidenceSlot("补充证据", [], [], 0), chunk));
            }
        }

        var selectedChunks = selectedBySlot
            .Select(item => item.Chunk)
            .DistinctBy(BuildChunkKey, StringComparer.Ordinal)
            .OrderBy(chunk => chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(chunk => chunk.Index)
            .ToArray();
        var coveredSlots = selectedBySlot
            .Where(item => item.Slot.MinimumMatches > 0 && ChunkMatchesEvidenceSlot(item.Chunk, item.Slot))
            .Select(item => item.Slot.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return BuildEvidenceSufficiencyResult(evidencePlan, selectedChunks, coveredSlots);
    }

    private static EvidenceSet BuildEvidenceSufficiencyResult(EvidencePlan evidencePlan, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> coveredSlots)
    {
        var isSufficient = evidencePlan.Mode switch
        {
            EvidenceMode.AbsenceCheck => chunks.Count == 0 || !HasTopicalEvidence(evidencePlan, chunks),
            EvidenceMode.Composition or EvidenceMode.Implementation => coveredSlots.Count >= Math.Min(3, evidencePlan.RequiredSlots.Count),
            EvidenceMode.ModuleList => coveredSlots.Count >= Math.Min(2, evidencePlan.RequiredSlots.Count),
            EvidenceMode.Definition => chunks.Count >= 1,
            _ => chunks.Count >= 1
        };

        if (evidencePlan.Mode == EvidenceMode.AbsenceCheck)
        {
            return isSufficient
                ? new EvidenceSet([], [], true, "我不知道（当前文档未覆盖）。")
                : new EvidenceSet(chunks, coveredSlots, true, null);
        }

        return isSufficient
            ? new EvidenceSet(chunks, coveredSlots, true, null)
            : new EvidenceSet(chunks, coveredSlots, false, "我不知道（当前文档未覆盖）。");
    }

    private static bool HasTopicalEvidence(EvidencePlan evidencePlan, IReadOnlyList<DocumentChunk> chunks)
    {
        var tokens = evidencePlan.QueryProfile.FocusTerms
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        if (tokens.Length == 0)
        {
            return chunks.Count > 0;
        }

        return chunks.Any(chunk =>
        {
            var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
            return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
        });
    }

    private static EvidenceMode MapEvidenceMode(string intent)
    {
        return intent switch
        {
            "summary" => EvidenceMode.Summary,
            "procedure" or "module_implementation" => EvidenceMode.Implementation,
            "composition" => EvidenceMode.Composition,
            "module_list" => EvidenceMode.ModuleList,
            "definition" => EvidenceMode.Definition,
            "compare" => EvidenceMode.Compare,
            "metadata" => EvidenceMode.Metadata,
            "usage" => EvidenceMode.Usage,
            "explain" => EvidenceMode.Explain,
            _ => EvidenceMode.General
        };
    }

    private static IReadOnlyList<EvidenceSlot> BuildEvidenceSlots(QueryProfile queryProfile, EvidenceMode mode)
    {
        return mode switch
        {
            EvidenceMode.Composition when ContainsAny(queryProfile.OriginalQuestion, "硬件", "设备", "部件", "器件", "元件") =>
            [
                new EvidenceSlot("主控/处理", ["主控", "芯片", "控制器", "处理器", "mcu", "单片机", "核心"], ["主控", "决策层"], 1),
                new EvidenceSlot("传感/采集", ["传感器", "采集", "检测", "湿度", "温度", "感知"], ["感知层", "采集"], 1),
                new EvidenceSlot("执行/驱动", ["执行", "驱动", "执行器", "阀", "电机", "继电器", "pwm", "输出"], ["执行层", "驱动"], 1),
                new EvidenceSlot("显示/交互", ["显示", "oled", "屏", "交互", "状态"], ["交互层", "显示"], 1),
                new EvidenceSlot("通信/联网", ["通信", "wifi", "esp", "串口", "联网", "协议"], ["通信", "远程"], 1)
            ],
            EvidenceMode.Composition =>
            [
                new EvidenceSlot("总体架构", ["架构", "总体", "系统", "分层", "组成", "前后端分离", "b/s", "restful", "api"], ["总体设计", "架构"], 1),
                new EvidenceSlot("后端/服务", ["后端", "服务", "接口", "api", "控制器", "服务端", "业务层", "框架"], ["后端", "服务"], 1),
                new EvidenceSlot("前端/交互", ["前端", "界面", "页面", "组件", "展示", "客户端", "交互", "路由"], ["前端", "交互"], 1),
                new EvidenceSlot("数据/存储", ["数据库", "存储", "持久化", "数据表", "缓存", "配置文件", "结构化数据"], ["数据", "存储"], 1),
                new EvidenceSlot("可视化/输出", ["可视化", "图谱", "图表", "输出", "展示", "渲染", "报表"], ["可视化", "输出"], 1)
            ],
            EvidenceMode.Implementation =>
            [
                new EvidenceSlot("输入/前提", ["输入", "采集", "获取", "接收", "检测", "监测", "读取", "数据", "参数", "状态", "条件"], ["输入", "采集", "感知", "前提"], 1),
                new EvidenceSlot("核心处理/决策", ["处理", "计算", "判断", "决策", "规则", "算法", "模型", "策略", "控制", "生成"], ["处理", "决策", "算法", "策略", "控制"], 1),
                new EvidenceSlot("过程/步骤", ["首先", "然后", "再", "最后", "步骤", "流程", "阶段", "过程", "链路", "转换"], ["流程", "步骤", "阶段", "过程"], 1),
                new EvidenceSlot("输出/执行", ["输出", "执行", "驱动", "控制", "发送", "下发", "展示", "显示", "更新", "存储", "反馈"], ["输出", "执行", "展示", "反馈"], 1),
                new EvidenceSlot("约束/补充机制", ["补偿", "校正", "优化", "异常", "阈值", "条件", "约束", "实时", "动态", "反馈"], ["补偿", "优化", "约束", "机制"], 0)
            ],
            EvidenceMode.ModuleList =>
            [
                new EvidenceSlot("模块名", ["模块", "功能模块", "子系统", "包括", "包含"], ["模块", "功能"], 1),
                new EvidenceSlot("模块职责", ["负责", "用于", "实现", "功能", "职责"], ["模块", "功能"], 1),
                new EvidenceSlot("模块关系", ["通信", "调用", "数据", "流程", "关系"], ["架构", "流程"], 1)
            ],
            EvidenceMode.Definition =>
            [
                new EvidenceSlot("定义/身份", ["是", "是指", "是一种", "定义", "称为", "属于", "作为"], ["定义", "概念", "简介"], 1),
                new EvidenceSlot("作用/职责", ["用于", "负责", "实现", "支持", "提供", "完成", "作用"], ["作用", "功能", "职责"], 0),
                new EvidenceSlot("上下位关系", ["包括", "包含", "组成", "模块", "部分", "类别", "类型"], ["组成", "架构", "模块"], 0)
            ],
            EvidenceMode.Compare =>
            [
                new EvidenceSlot("比较对象", ["区别", "不同", "差异", "相比", "对比", "分别"], ["对比", "比较", "区别"], 1),
                new EvidenceSlot("比较维度", ["阶段", "功能", "作用", "特点", "优势", "不足", "范围", "方式"], ["阶段", "特征", "功能"], 1)
            ],
            _ => []
        };
    }

    private static IReadOnlyList<string> BuildPreferredSectionSignals(EvidenceMode mode, string question)
    {
        return mode switch
        {
            EvidenceMode.Summary => ["摘要", "abstract", "overview", "概述", "结论"],
            EvidenceMode.Implementation => ["方法", "控制", "流程", "阶段", "策略", "机制", "调节", "补偿"],
            EvidenceMode.Composition => ["总体设计", "总体架构", "架构", "技术栈", "前后端", "组成", "模块", "硬件", "软件", "接口", "数据库", "可视化"],
            EvidenceMode.ModuleList => ["模块", "功能", "系统设计", "架构"],
            EvidenceMode.Metadata => ["title", "abstract", "keywords", "作者", "摘要", "关键词"],
            EvidenceMode.AbsenceCheck => ExtractQueryTokens(question).Where(IsMeaningfulFocusTerm).ToArray(),
            _ => []
        };
    }

    private static string BuildSufficiencyRule(EvidenceMode mode)
    {
        return mode switch
        {
            EvidenceMode.Composition => "cover-at-least-three-composition-slots",
            EvidenceMode.Implementation => "cover-input-decision-adjustment-execution-slots",
            EvidenceMode.ModuleList => "cover-module-name-and-duty",
            EvidenceMode.AbsenceCheck => "unknown-when-no-topical-evidence-in-scope",
            EvidenceMode.Definition => "at-least-one-definition-or-identity-evidence",
            _ => "at-least-one-allowed-evidence"
        };
    }

    private static float ComputeEvidencePolicyBoost(EvidencePlan evidencePlan, DocumentChunk chunk)
    {
        var boost = 0f;
        if (!IsChunkAllowedByEvidencePlan(evidencePlan, chunk))
        {
            return -1.2f;
        }

        if (evidencePlan.SourcePreference == "structured-json" && IsJsonChunk(chunk))
        {
            boost += 0.12f;
        }

        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        var preferredHits = evidencePlan.PreferredSections.Count(signal => structureText.Contains(signal, StringComparison.OrdinalIgnoreCase));
        boost += Math.Min(0.30f, preferredHits * 0.08f);

        if (evidencePlan.Mode is EvidenceMode.Implementation && chunk.ContentKind == "summary")
        {
            boost -= 0.20f;
        }

        if (evidencePlan.Mode is EvidenceMode.Summary && chunk.ContentKind == "summary")
        {
            boost += 0.10f;
        }

        return boost;
    }

    private static bool IsChunkAllowedByEvidencePlan(EvidencePlan evidencePlan, DocumentChunk chunk)
    {
        if (evidencePlan.DisallowedContentKinds.Any(kind =>
                kind.Equals("english", StringComparison.OrdinalIgnoreCase)
                    ? IsEnglishFieldChunk(chunk)
                    : string.Equals(kind, chunk.ContentKind, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (evidencePlan.AllowedContentKinds.Count == 0)
        {
            return true;
        }

        return evidencePlan.AllowedContentKinds.Any(kind => string.Equals(kind, chunk.ContentKind, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ChunkMatchesEvidenceSlot(DocumentChunk chunk, EvidenceSlot slot)
    {
        var corpus = BuildChunkRetrievalText(chunk);
        return slot.MatchTerms.Any(term => corpus.Contains(term, StringComparison.OrdinalIgnoreCase))
            || slot.PreferredSections.Any(term => $"{chunk.SectionTitle} {chunk.StructurePath}".Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static float ScoreChunkForEvidenceSlot(DocumentChunk chunk, EvidenceSlot slot, EvidencePlan evidencePlan, QueryProfile queryProfile)
    {
        var corpus = BuildChunkRetrievalText(chunk);
        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        var termHits = slot.MatchTerms.Count(term => corpus.Contains(term, StringComparison.OrdinalIgnoreCase));
        var sectionHits = slot.PreferredSections.Count(term => structureText.Contains(term, StringComparison.OrdinalIgnoreCase));
        return (termHits * 2f)
            + (sectionHits * 3f)
            + ComputeEvidencePolicyBoost(evidencePlan, chunk)
            + ComputeCoverageScore(queryProfile.FocusTerms, chunk)
            + ComputeTitleScore(queryProfile.FocusTerms, chunk)
            + ComputeJsonStructureScore(queryProfile.FocusTerms, chunk)
            + (chunk.ContentKind == "body" ? 0.5f : 0f);
    }

    private static bool IsAbsenceCheckQuestion(string question)
    {
        return ContainsAny(question, "有写", "有没有", "是否提到", "是否涉及", "提到过", "未提及", "没有提到");
    }

    private static bool ShouldUseActiveDocumentScope(QueryProfile queryProfile)
    {
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle))
        {
            return false;
        }

        return queryProfile.OriginalQuestion.Contains("这篇", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("该文档", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("文档里", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("文中", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("这个系统", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("详细一点", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("具体一点", StringComparison.Ordinal);
    }

    private void RememberActiveDocumentScope(EvidenceSet evidenceSet)
    {
        var paths = evidenceSet.Chunks
            .GroupBy(chunk => chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .Take(2)
            .ToArray();
        if (paths.Length > 0)
        {
            _activeDocumentFilePaths = paths;
        }
    }

    private static string BuildChunkKey(DocumentChunk chunk)
    {
        return $"{chunk.FilePath}#{chunk.Index}";
    }

    private void AddJsonBranchContext(
        Dictionary<string, DocumentChunk> selected,
        DocumentChunk seedChunk,
        IReadOnlyList<string> focusTerms,
        IReadOnlySet<string>? targetFilePathSet)
    {
        if (!IsJsonChunk(seedChunk) || string.IsNullOrWhiteSpace(seedChunk.StructurePath))
        {
            return;
        }

        var branchLimit = Math.Max(1, _options.ContextWindowRadius + 1);
        var branchCandidates = _indexedChunks
            .Where(item => string.Equals(item.Chunk.FilePath, seedChunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Chunk.Index != seedChunk.Index)
            .Where(item => targetFilePathSet is null || targetFilePathSet.Contains(item.Chunk.FilePath))
            .Where(item => AreJsonChunksStructurallyRelated(seedChunk, item.Chunk))
            .Select(item => new
            {
                item.Chunk,
                Score = ComputeCoverageScore(focusTerms, item.Chunk) + ComputeJsonStructureScore(focusTerms, item.Chunk)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Take(branchLimit)
            .Select(item => item.Chunk);

        foreach (var chunk in branchCandidates)
        {
            var key = $"{chunk.FilePath}#{chunk.Index}";
            if (!selected.ContainsKey(key))
            {
                selected.Add(key, chunk);
            }
        }
    }

    private void AddSectionRelatedContext(
        Dictionary<string, DocumentChunk> selected,
        DocumentChunk seedChunk,
        QueryProfile queryProfile,
        IReadOnlySet<string>? targetFilePathSet)
    {
        if (string.IsNullOrWhiteSpace(seedChunk.StructurePath))
        {
            return;
        }

        var parentPath = GetParentStructurePath(seedChunk.StructurePath);
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            parentPath = seedChunk.StructurePath;
        }

        var limit = queryProfile.Intent is "composition" or "procedure" or "module_list" or "module_implementation"
            ? 4
            : 2;
        var related = _indexedChunks
            .Where(item => string.Equals(item.Chunk.FilePath, seedChunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Chunk.Index != seedChunk.Index)
            .Where(item => targetFilePathSet is null || targetFilePathSet.Contains(item.Chunk.FilePath))
            .Where(item => IsSectionRelated(seedChunk, item.Chunk, parentPath))
            .Where(item => item.Chunk.ContentKind is "body" or "summary" or "")
            .Select(item => new
            {
                item.Chunk,
                Score = ScoreSectionRelatedChunk(item.Chunk, queryProfile)
            })
            .Where(item => item.Score > 0f)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Take(limit)
            .Select(item => item.Chunk);

        foreach (var chunk in related)
        {
            var key = $"{chunk.FilePath}#{chunk.Index}";
            if (!selected.ContainsKey(key))
            {
                selected.Add(key, chunk);
            }
        }
    }

    private static bool IsSectionRelated(DocumentChunk seedChunk, DocumentChunk candidate, string parentPath)
    {
        if (string.Equals(candidate.StructurePath, seedChunk.StructurePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(parentPath)
            && candidate.StructurePath.StartsWith(parentPath + " > ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Math.Abs(candidate.Index - seedChunk.Index) <= 2;
    }

    private static float ScoreSectionRelatedChunk(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var score = ComputeCoverageScore(queryProfile.FocusTerms, chunk)
            + ComputeTitleScore(queryProfile.FocusTerms, chunk)
            + ComputeJsonStructureScore(queryProfile.FocusTerms, chunk);
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";

        score += queryProfile.Intent switch
        {
            "composition" => Math.Min(0.8f, (CountCompositionStructureSignals(source) + CountGenericAnswerEntities(source)) * 0.08f),
            "procedure" or "module_implementation" => Math.Min(0.8f, (CountProcedureStructureSignals(source) + CountMethodSignals(source)) * 0.08f),
            "module_list" => Math.Min(0.8f, CountSystemModuleSignals(source) * 0.08f),
            _ => Math.Min(0.4f, CountGenericAnswerEntities(source) * 0.05f)
        };

        if (chunk.ContentKind == "body")
        {
            score += 0.15f;
        }

        return score;
    }

    private void AddJsonProcedureChapterContext(
        Dictionary<string, DocumentChunk> selected,
        DocumentChunk seedChunk,
        IReadOnlyList<string> focusTerms,
        IReadOnlySet<string>? targetFilePathSet)
    {
        if (!TryFindProcedureAnchorPath(seedChunk, focusTerms, out var anchorPath))
        {
            return;
        }

        var chapterCandidates = _indexedChunks
            .Where(item => string.Equals(item.Chunk.FilePath, seedChunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Chunk.Index != seedChunk.Index)
            .Where(item => targetFilePathSet is null || targetFilePathSet.Contains(item.Chunk.FilePath))
            .Where(item => item.Chunk.StructurePath.StartsWith(anchorPath + " > ", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Chunk.ContentKind is "body" or "summary" or "")
            .Select(item => new
            {
                item.Chunk,
                Score = ComputeCoverageScore(focusTerms, item.Chunk)
                    + ComputeTitleScore(focusTerms, item.Chunk)
                    + ComputeJsonStructureScore(focusTerms, item.Chunk)
                    + CountSignals($"{item.Chunk.SectionTitle} {item.Chunk.StructurePath}", "阶段", "策略", "机制", "补偿", "调节", "控制") * 0.08f
            })
            .Where(item => item.Score > 0f)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Take(Math.Max(3, _options.ContextWindowRadius + 3))
            .Select(item => item.Chunk);

        foreach (var chunk in chapterCandidates)
        {
            var key = $"{chunk.FilePath}#{chunk.Index}";
            if (!selected.ContainsKey(key))
            {
                selected.Add(key, chunk);
            }
        }
    }

    private static bool TryFindProcedureAnchorPath(DocumentChunk chunk, IReadOnlyList<string> focusTerms, out string anchorPath)
    {
        anchorPath = string.Empty;
        if (!IsJsonChunk(chunk) || string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            return false;
        }

        var segments = chunk.StructurePath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        for (var index = 0; index < segments.Length; index++)
        {
            if (!IsProcedureAnchorSegment(segments[index], focusTerms))
            {
                continue;
            }

            anchorPath = string.Join(" > ", segments[..(index + 1)]);
            return true;
        }

        return false;
    }

    private static bool IsProcedureAnchorSegment(string segment, IReadOnlyList<string> focusTerms)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        if (ContainsAny(segment, "流程", "步骤", "阶段", "过程", "策略", "机制", "方法", "算法", "控制", "处理", "执行", "实现"))
        {
            return true;
        }

        return focusTerms.Any(term => term.Length >= 3 && segment.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static float ComputeRequestedDocumentScore(IReadOnlySet<string>? targetFilePathSet, string filePath)
    {
        if (targetFilePathSet is null || targetFilePathSet.Count == 0)
        {
            return 0f;
        }

        return targetFilePathSet.Contains(filePath) ? 0.12f : -0.10f;
    }

    private static bool AreJsonChunksStructurallyRelated(DocumentChunk left, DocumentChunk right)
    {
        if (!IsJsonChunk(left) || !IsJsonChunk(right))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(left.StructurePath) || string.IsNullOrWhiteSpace(right.StructurePath))
        {
            return false;
        }

        if (string.Equals(left.StructurePath, right.StructurePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leftParent = GetParentStructurePath(left.StructurePath);
        var rightParent = GetParentStructurePath(right.StructurePath);
        if (!string.IsNullOrWhiteSpace(leftParent)
            && string.Equals(leftParent, rightParent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return left.StructurePath.StartsWith(right.StructurePath + " > ", StringComparison.OrdinalIgnoreCase)
            || right.StructurePath.StartsWith(left.StructurePath + " > ", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetParentStructurePath(string structurePath)
    {
        if (string.IsNullOrWhiteSpace(structurePath))
        {
            return string.Empty;
        }

        var segments = structurePath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return string.Empty;
        }

        return string.Join(" > ", segments[..^1]);
    }

    private static bool IsJsonChunk(DocumentChunk chunk)
    {
        return chunk.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var character in text)
        {
            if (character is >= '\u4e00' and <= '\u9fff')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsChineseQuestion(string question)
    {
        return !string.IsNullOrWhiteSpace(question) && question.Any(character => character is >= '\u4e00' and <= '\u9fff');
    }

    private static bool IsEnglishFieldChunk(DocumentChunk chunk)
    {
        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        return structureText.Contains("english", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldKeepChunkForAnswer(QueryProfile queryProfile, DocumentChunk chunk)
    {
        if (queryProfile.Intent == "metadata")
        {
            return true;
        }

        if (chunk.ContentKind == "metadata")
        {
            return false;
        }

        if (!IsChineseQuestion(queryProfile.OriginalQuestion))
        {
            return true;
        }

        if (IsEnglishFieldChunk(chunk))
        {
            return false;
        }

        var cleanedText = SanitizeChunkForPrompt(chunk.Text);
        var hasCjk = ContainsCjk(cleanedText);
        return hasCjk || cleanedText.Length < 40;
    }

    private string BuildPrompt(
        string question,
        IReadOnlyList<ChatTurn> history,
        QueryProfile queryProfile,
        IReadOnlyList<ScoredChunk> rankedChunks,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是一个本地文档问答助手。你必须严格基于下方“上下文”回答，不得使用外部常识扩写。");
        builder.AppendLine("回答规则：");
        builder.AppendLine("1) 若上下文不足或不包含答案，直接回答“我不知道（当前文档未覆盖）”。");
        builder.AppendLine("2) 每个自然段或每个要点末尾都要标注 1-2 个稳定来源标签，直接复制上下文中的“来源标签”，例如 [doc/a.md | 方法设计 | c3]。禁止只写 [1]、[2] 这种临时编号，也禁止编造未在上下文中出现过的标签。");
        builder.AppendLine($"3) {BuildAnswerStructureRule(queryProfile)}");
        builder.AppendLine("4) 禁止直接输出论文题目、作者名单、单位信息、HTML/Markdown 标题块。不要把 JSON 字段名前缀如 title: / abstract: / content: 原样抄进答案。");
        builder.AppendLine($"5) {BuildCrossSourceSynthesisRule(queryProfile)}");
        builder.AppendLine("6) 回答语言必须与用户问题一致：中文问题必须用中文回答。");
        builder.AppendLine($"7) 当前问题类型: {DescribeIntent(queryProfile.Intent)}。请按这个类型组织答案。");
        builder.AppendLine($"8) {BuildDomainGuidance(queryProfile)}");
        builder.AppendLine("9) 优先写成连贯自然段，避免机械地逐条摘抄上下文。除非问题本身要求清单，否则不要堆很多短 bullet。");
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) && targetFilePaths.Count > 0)
        {
            builder.AppendLine($"10) 用户明确在问《{queryProfile.RequestedDocumentTitle}》相关内容。除非上下文明确要求跨文档补充，否则优先只使用该文档来源，不要跳到其他文档。");
        }
        builder.AppendLine();
        AppendConversationHistory(builder, history, question);
        builder.AppendLine("优先关注的证据:");
        for (var index = 0; index < rankedChunks.Count; index++)
        {
            var item = rankedChunks[index];
            builder.AppendLine($"- {BuildStableCitation(item.Chunk)} {item.Chunk.DocumentTitle} / {item.Chunk.SectionTitle}");
        }

        builder.AppendLine();
        builder.AppendLine("上下文:");

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"来源标签: {BuildStableCitation(chunk)}");
            builder.AppendLine($"来源说明: {chunk.DocumentTitle} / {chunk.SectionTitle} ({chunk.FilePath})");
            if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
            {
                builder.AppendLine($"结构路径: {chunk.StructurePath}");
            }
            builder.AppendLine(SanitizeChunkForPrompt(chunk.Text));
            builder.AppendLine();
        }

        builder.AppendLine($"用户问题: {question}");
        builder.Append("助手回答:");
        return builder.ToString();
    }

    private static void AppendConversationHistory(StringBuilder builder, IReadOnlyList<ChatTurn> history, string currentQuestion)
    {
        var recentHistory = history
            .Where(turn => turn.Role is "用户" or "助手")
            .Reverse()
            .Where(turn => !turn.IsUser || !string.Equals(turn.Content.Trim(), currentQuestion.Trim(), StringComparison.Ordinal))
            .Take(6)
            .Reverse()
            .ToArray();

        if (recentHistory.Length == 0)
        {
            return;
        }

        builder.AppendLine("最近对话（仅用于理解省略指代，最终答案仍必须严格基于下方上下文）:");
        foreach (var turn in recentHistory)
        {
            builder.AppendLine($"{turn.Role}: {TruncateForPrompt(turn.Content, 500)}");
        }

        builder.AppendLine();
    }

    private static string TruncateForPrompt(string text, int maxLength)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private string BuildRepairPrompt(
        string question,
        QueryProfile queryProfile,
        IReadOnlyList<ScoredChunk> rankedChunks,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是本地文档问答助手。上一次回答偏离问题。现在必须重新回答。\n");
        builder.AppendLine("硬性要求：");
        builder.AppendLine($"1) 仅依据下面上下文回答，不要输出与问题或者给定材料无关的题目模板、算法示例、代码题。\n2) 禁止输出论文题目、作者名单、单位信息、HTML/Markdown 标题块，也不要把 title:/abstract:/content: 等字段名前缀原样抄进答案。\n3) 每个自然段或每个要点末尾必须附稳定来源标签，且只能复制上下文中已经出现过的“来源标签”，例如 [doc/a.md | 方法设计 | c3]。\n4) 回答语言必须与用户问题一致（中文问中文答）。\n5) {BuildAnswerStructureRule(queryProfile)}\n6) {BuildDomainGuidance(queryProfile)}\n7) {BuildCrossSourceSynthesisRule(queryProfile)}\n8) 若上下文不足，仅输出：我不知道（当前文档未覆盖）。");
        builder.AppendLine("9) 优先写成连贯自然段，减少机械罗列；只有在问题明确要求步骤或清单时才使用编号。");
        if (!string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) && targetFilePaths.Count > 0)
        {
            builder.AppendLine($"10) 用户明确在问《{queryProfile.RequestedDocumentTitle}》，本次回答优先使用该文档来源，不要跳到其他文档。");
        }
        builder.AppendLine($"当前问题类型: {DescribeIntent(queryProfile.Intent)}");
        if (rankedChunks.Count > 0)
        {
            builder.AppendLine("优先参考高分证据: " + string.Join("；", rankedChunks.Select(item => $"{BuildStableCitation(item.Chunk)} {item.Chunk.DocumentTitle}/{item.Chunk.SectionTitle}")));
        }
        builder.AppendLine();
        builder.AppendLine("上下文:");
        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            builder.AppendLine($"来源标签: {BuildStableCitation(chunk)}");
            builder.AppendLine($"来源说明: {chunk.DocumentTitle} / {chunk.SectionTitle} ({chunk.FilePath})");
            if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
            {
                builder.AppendLine($"结构路径: {chunk.StructurePath}");
            }
            builder.AppendLine(SanitizeChunkForPrompt(chunk.Text));
            builder.AppendLine();
        }

        builder.AppendLine($"用户问题: {question}");
        builder.Append("助手回答:");
        return builder.ToString();
    }

    private static string DescribeIntent(string intent)
    {
        return intent switch
        {
            "module_list" => "模块清单题，列出核心功能模块并保留模块名",
            "module_implementation" => "模块实现题，围绕目标模块说明通信链路、显示/控制环节和执行方式",
            "metadata" => "元数据题，按字段逐项给出标题、摘要、关键词等明确字段值",
            "composition" => "组成/配置题，先说整体，再列组成项或条件",
            "compare" => "对比题，先说主要差异，再分点比较",
            "explain" => "解释题，先说结论，再说明原因/机制",
            "procedure" => "流程题，按步骤说明",
            "usage" => "用途题，先说用途，再列关键点",
            "summary" => "总结题，先概括主题，再分层展开内容",
            "definition" => "定义题，先给定义，再补充特征或作用",
            _ => "一般问答，先结论后依据"
        };
    }

    private bool IsOffTopicOrMalformedAnswer(string answer, string question, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }

        var normalized = answer.Trim();
        if (normalized.Contains("Problem:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Constraints:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Approach:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("nums =", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("target =", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("two numbers", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HasTitleOrAuthorBlock(normalized))
        {
            return true;
        }

        // 新增：判定全英文摘要片段或无关英文内容
        if (IsLikelyEnglishAbstract(normalized, question))
        {
            return true;
        }

        if (!CitationRegex.IsMatch(normalized))
        {
            return true;
        }

        if (!ContainsOnlyKnownCitations(normalized, chunks))
        {
            return true;
        }

        if (targetFilePaths.Count > 0 && !ContainsAnyCitationFromTargetDocument(normalized, chunks, targetFilePaths))
        {
            return true;
        }

        if ((question.Contains("为什么", StringComparison.Ordinal)
            || question.Contains("如何", StringComparison.Ordinal)
            || question.Contains("怎么", StringComparison.Ordinal)
            || question.Contains("总结", StringComparison.Ordinal)
            || question.Contains("概述", StringComparison.Ordinal))
            && normalized.Length < 40)
        {
            return true;
        }

        if (WantsDetailedAnswer(question) && normalized.Length < 120)
        {
            return true;
        }

        if (IsUsageQuestion(question) && IsGenericUsageAnswer(normalized))
        {
            return true;
        }

        if (question.Contains("英文标题", StringComparison.Ordinal)
            || question.Contains("英文摘要", StringComparison.Ordinal)
            || question.Contains("英文关键词", StringComparison.Ordinal))
        {
            var englishCheckAnswer = normalized.ToLowerInvariant();
            if (!englishCheckAnswer.Contains("english", StringComparison.Ordinal)
                && !ContainsOnlyAsciiOrPunctuation(normalized))
            {
                return true;
            }
        }

        if (IsModuleImplementationQuestion(question) && !HasGenericModuleImplementationAnswerShape(normalized))
        {
            return true;
        }

        if (IsReferenceStyleAnswer(normalized))
        {
            return true;
        }

        var requestedDocumentTitle = ExtractRequestedDocumentTitle(question);
        var meaningfulTokens = BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(IsMeaningfulFocusTerm)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (meaningfulTokens.Length == 0)
        {
            return false;
        }

        var lowerAnswer = normalized.ToLowerInvariant();
        if (IsSummaryQuestion(question) && meaningfulTokens.Length <= 2)
        {
            return false;
        }

        var overlap = meaningfulTokens.Count(token => lowerAnswer.Contains(token, StringComparison.Ordinal));
        return overlap == 0;
    }

    private static bool ContainsOnlyAsciiOrPunctuation(string text)
    {
        foreach (var character in text)
        {
            if (character is >= '\u4e00' and <= '\u9fff')
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldReturnUnknownForUnsupportedRequestedDocumentTopic(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        if (string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle) || chunks.Count == 0)
        {
            return false;
        }

        if (queryProfile.Intent is "summary" or "metadata")
        {
            return false;
        }

        var topicalTokens = BuildRetrievalQueryTokens(queryProfile.OriginalQuestion, queryProfile.RequestedDocumentTitle)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (topicalTokens.Length == 0)
        {
            return false;
        }

        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var scopedChunks = chunks
            .Where(chunk => targetFilePathSet is null || targetFilePathSet.Contains(chunk.FilePath))
            .ToArray();
        if (scopedChunks.Length == 0)
        {
            return false;
        }

        var subjectTerms = ExtractQuestionSubjectTerms(queryProfile.OriginalQuestion)
            .ToArray();
        if (HasAnyTermInChunks(subjectTerms, scopedChunks))
        {
            return false;
        }

        var combinedText = string.Join(
            "\n",
            scopedChunks
                .Select(BuildChunkRetrievalText)
                .Where(text => !string.IsNullOrWhiteSpace(text)))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return false;
        }

        var matchedTokenCount = topicalTokens.Count(token => combinedText.Contains(token, StringComparison.Ordinal));
        return matchedTokenCount == 0;
    }

    private static bool ContainsOnlyKnownCitations(string answer, IReadOnlyList<DocumentChunk> chunks)
    {
        var knownCitations = new HashSet<string>(
            chunks.Select(BuildStableCitation),
            StringComparer.Ordinal);

        var matches = CitationRegex.Matches(answer);
        if (matches.Count == 0)
        {
            return false;
        }

        foreach (Match match in matches)
        {
            if (!knownCitations.Contains(match.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsAnyCitationFromTargetDocument(string answer, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths)
    {
        var targetFilePathSet = new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var targetCitations = new HashSet<string>(
            chunks.Where(chunk => targetFilePathSet.Contains(chunk.FilePath)).Select(BuildStableCitation),
            StringComparer.Ordinal);

        if (targetCitations.Count == 0)
        {
            return true;
        }

        foreach (Match match in CitationRegex.Matches(answer))
        {
            if (targetCitations.Contains(match.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGenericUsageAnswer(string answer)
    {
        var normalized = answer.Trim();
        if (normalized.Length < 55)
        {
            return true;
        }

        var genericPhrases = new[]
        {
            "作为核心",
            "发挥作用",
            "核心单片机",
            "主要作用",
            "主要作为",
            "在系统中发挥",
            "在智能系统设计中"
        };

        var genericHits = genericPhrases.Count(phrase => normalized.Contains(phrase, StringComparison.Ordinal));
        var bulletLike = normalized.Contains("1.", StringComparison.Ordinal)
            || normalized.Contains("2.", StringComparison.Ordinal)
            || normalized.Contains("①", StringComparison.Ordinal)
            || normalized.Contains("②", StringComparison.Ordinal)
            || normalized.Contains("•", StringComparison.Ordinal);

        return genericHits >= 2 && !bulletLike;
    }

    // 判定英文摘要片段或无关英文内容
    private static bool IsLikelyEnglishAbstract(string text, string question)
    {
        // 只要有较多英文单词、无中文标点、无“作用/用途/功能”等关键词，且长度大于20
        if (string.IsNullOrWhiteSpace(text) || text.Length < 20)
            return false;

        var hasChinese = text.Any(c => c >= '\u4e00' && c <= '\u9fff');
        var questionHasChinese = !string.IsNullOrWhiteSpace(question) && question.Any(c => c >= '\u4e00' && c <= '\u9fff');
        if (hasChinese)
            return false;

        var hasEnglish = text.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
        if (!hasEnglish)
            return false;

        // 中文问题却返回全英文，大概率是跑偏。
        if (questionHasChinese && !hasChinese)
        {
            return true;
        }

        // 没有中文句号/逗号/用途类词
        var hasCnPunct = text.Contains('。') || text.Contains('，') || text.Contains('：');
        if (hasCnPunct)
            return false;

        // 问题里如果有“作用/用途/功能”，但回答里没有这些词，也判为无效
        var q = question ?? string.Empty;
        if ((q.Contains("作用") || q.Contains("用途") || q.Contains("功能")) &&
            !(text.Contains("作用") || text.Contains("用途") || text.Contains("功能")))
        {
            return true;
        }

        // 纯英文长句，且有 design/system/was/used/implemented 等典型摘要词
        var lower = text.ToLowerInvariant();
        if (lower.Contains("was designed") || lower.Contains("system based on") || lower.Contains("implemented") || lower.Contains("is proposed") || lower.Contains("is used") || lower.Contains("microcontroller"))
        {
            return true;
        }

        // 句子全英文且无中文，且长度较长
        if (text.Length > 40 && !hasChinese && hasEnglish)
            return true;

        return false;
    }

    private static bool IsReferenceStyleAnswer(string answer)
    {
        var lines = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return true;
        }

        var referenceLikeLines = lines.Count(IsReferenceLikeSentence);
        return referenceLikeLines * 2 >= lines.Length;
    }

    private static bool HasGenericModuleImplementationAnswerShape(string answer)
    {
        var normalized = Regex.Replace(answer.ToLowerInvariant(), "\\s+", string.Empty);
        return ContainsAny(normalized, "通过", "采用", "连接", "接口", "协议", "通信", "链路", "上传", "下发", "显示", "控制", "实现", "处理", "调用", "模块");
    }

    private string? BuildMetadataFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        if (!EnglishMetadataSignals.Any(signal => question.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var titleChunk = chunks.FirstOrDefault(chunk =>
            chunk.StructurePath.Contains("englishTitle", StringComparison.OrdinalIgnoreCase)
            || chunk.SectionTitle.Contains("englishTitle", StringComparison.OrdinalIgnoreCase));
        var abstractChunk = chunks.FirstOrDefault(chunk =>
            chunk.StructurePath.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase)
            || chunk.SectionTitle.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase));
        var keywordChunk = chunks.FirstOrDefault(chunk =>
            chunk.StructurePath.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase)
            || chunk.SectionTitle.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase));

        if (titleChunk is null && abstractChunk is null && keywordChunk is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        if (titleChunk is not null)
        {
            builder.AppendLine($"英文标题：{ExtractFieldValue(titleChunk.Text)} {BuildStableCitation(titleChunk)}");
        }

        if (abstractChunk is not null)
        {
            builder.AppendLine($"英文摘要：{ExtractFieldValue(abstractChunk.Text)} {BuildStableCitation(abstractChunk)}");
        }

        if (keywordChunk is not null)
        {
            builder.AppendLine($"英文关键词：{ExtractKeywordList(keywordChunk.Text)} {BuildStableCitation(keywordChunk)}");
        }

        var answer = builder.ToString().TrimEnd();
        return string.IsNullOrWhiteSpace(answer) ? null : answer;
    }

    private static string ExtractFieldValue(string text)
    {
        var cleaned = CleanStructuredSentence(text);
        var separatorIndex = cleaned.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            separatorIndex = cleaned.IndexOf('：');
        }

        return separatorIndex >= 0 ? cleaned[(separatorIndex + 1)..].Trim() : cleaned;
    }

    private static string ExtractKeywordList(string text)
    {
        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', ' ', '\t'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return ExtractFieldValue(text);
        }

        return string.Join(", ", lines);
    }

    private static string JoinSentencesWithCitation(IEnumerable<string> sentences, string lead, string citation)
    {
        var parts = sentences
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (parts.Length == 0)
        {
            return string.Empty;
        }

        return $"{lead}{string.Join("，", parts)} {citation}";
    }

    private string BuildExtractiveFallbackAnswer(string question, QueryProfile queryProfile, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths)
    {
        var scopedChunks = FilterChunksByTargetFiles(chunks, targetFilePaths)
            .Where(chunk => ShouldKeepChunkForAnswer(queryProfile, chunk))
            .ToArray();
        var scopedCorpusChunks = FilterChunksByTargetFiles(_chunks, targetFilePaths)
            .Where(chunk => ShouldKeepChunkForAnswer(queryProfile, chunk))
            .ToArray();

        if (queryProfile.Intent == "metadata")
        {
            var metadata = BuildMetadataFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(metadata))
            {
                return metadata;
            }
        }

        if (IsModuleQuestion(question))
        {
            var moduleAnswer = BuildModuleFallbackAnswer(scopedChunks, scopedCorpusChunks);
            if (!string.IsNullOrWhiteSpace(moduleAnswer))
            {
                return moduleAnswer;
            }
        }

        if (queryProfile.Intent == "composition")
        {
            var composition = BuildCompositionFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(composition))
            {
                return composition;
            }
        }

        if (queryProfile.Intent == "definition")
        {
            var definition = BuildDefinitionFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(definition))
            {
                return definition;
            }
        }

        if (queryProfile.Intent == "module_implementation")
        {
            var moduleImplementation = BuildModuleImplementationFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(moduleImplementation))
            {
                return moduleImplementation;
            }
        }

        if (queryProfile.Intent == "compare")
        {
            var comparison = BuildCompareFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(comparison))
            {
                return comparison;
            }
        }

        if (queryProfile.Intent == "explain")
        {
            var explanation = BuildExplanationFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(explanation))
            {
                return explanation;
            }
        }

        if (IsPaperChallengeAndFutureQuestion(question))
        {
            var challengeAnswer = BuildPaperChallengeAndFutureFallbackAnswer(scopedChunks);
            if (!string.IsNullOrWhiteSpace(challengeAnswer))
            {
                return challengeAnswer;
            }
        }

        // 对“作用/用途/功能”类问题走专门的结构化回退，避免残句和题头片段
        if (IsUsageQuestion(question))
        {
            var usage = BuildUsageFallbackAnswer(question, scopedChunks, scopedCorpusChunks);
            if (!string.IsNullOrWhiteSpace(usage))
            {
                return usage;
            }
        }

        if (queryProfile.Intent == "procedure")
        {
            var procedure = BuildProcedureFallbackAnswer(question, scopedChunks);
            if (!string.IsNullOrWhiteSpace(procedure))
            {
                return procedure;
            }
        }

        if (queryProfile.Intent == "summary")
        {
            if (IsPaperQuestion(queryProfile.OriginalQuestion))
            {
                var paperSummary = BuildPaperSummaryFallbackAnswer(scopedChunks, queryProfile.WantsDetailedAnswer);
                if (!string.IsNullOrWhiteSpace(paperSummary))
                {
                    return paperSummary;
                }
            }

            var summary = BuildSummaryFallbackAnswer(scopedChunks, queryProfile.WantsDetailedAnswer);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }

        var tokens = ExtractQueryTokens(question);
        var candidates = new List<(DocumentChunk Chunk, string Sentence, int Score)>();

        for (var sourceIndex = 0; sourceIndex < scopedChunks.Length; sourceIndex++)
        {
            var chunk = scopedChunks[sourceIndex];
            var sentences = GetCleanSentences(chunk.Text, 6);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence))
                {
                    continue;
                }

                var lowerSentence = sentence.ToLowerInvariant();
                var score = tokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                if (score > 0)
                {
                    candidates.Add((chunk, sentence, score));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return "我不知道（当前文档未覆盖）。";
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .Take(queryProfile.WantsDetailedAnswer ? Math.Max(_options.FallbackSentenceCount, 4) : _options.FallbackSentenceCount)
            .ToArray();

        var builder = new StringBuilder();
        for (var index = 0; index < selected.Length; index++)
        {
            var item = selected[index];
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append($"{item.Sentence} {BuildStableCitation(item.Chunk)}");
        }

        return builder.ToString();
    }

    private string? BuildPaperSummaryFallbackAnswer(IReadOnlyList<DocumentChunk> chunks, bool wantsDetailedAnswer)
    {
        var documentChunks = GetPrimaryDocumentChunks(chunks);
        if (documentChunks.Count == 0)
        {
            return null;
        }

        var usedSentences = new HashSet<string>(StringComparer.Ordinal);
        var objective = SelectBestSummarySentence(
            documentChunks,
            chunk => chunk.ContentKind == "summary"
                || chunk.SectionTitle.Contains("引言", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("摘要", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("abstract", StringComparison.OrdinalIgnoreCase),
            sentence => CountSignals(sentence, "针对", "旨在", "设计", "目标", "解决", "提出", "实现", "系统"),
            usedSentences,
            minimumSignalScore: 2);

        var architecture = SelectBestSummarySentence(
            documentChunks,
            chunk => chunk.StructurePath.Contains("overview", StringComparison.OrdinalIgnoreCase)
                || chunk.StructurePath.Contains("architecture", StringComparison.OrdinalIgnoreCase)
                || chunk.StructurePath.Contains("workflow", StringComparison.OrdinalIgnoreCase)
                || chunk.SectionTitle.Contains("总体设计", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("系统", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("架构", StringComparison.Ordinal),
            sentence => CountSignals(sentence, "架构", "模块", "组成", "层", "流程", "设计", "包括", "包含", "系统"),
            usedSentences,
            minimumSignalScore: 2);

        var method = SelectBestSummarySentence(
            documentChunks,
            chunk => chunk.SectionTitle.Contains("控制", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("软件", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("控制", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("方法", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("机制", StringComparison.Ordinal),
            sentence => CountSignals(sentence, "方法", "算法", "控制", "调节", "机制", "流程", "采集", "处理", "生成", "实现"),
            usedSentences,
            minimumSignalScore: 2);

        var result = SelectBestSummarySentence(
            documentChunks,
            chunk => chunk.ContentKind == "summary"
                || chunk.SectionTitle.Contains("结论", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("测试", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("评估", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("节水", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("结论", StringComparison.Ordinal),
            sentence => CountSignals(sentence, "实验结果", "测试表明", "结果表明", "结论", "节水", "提升", "降低", "偏差", "适用", "推广", "价值", "解决方案"),
            usedSentences,
            minimumSignalScore: 2);

        var innovation = wantsDetailedAnswer
            ? SelectBestSummarySentence(
                documentChunks,
                chunk => chunk.SectionTitle.Contains("创新", StringComparison.Ordinal)
                    || chunk.StructurePath.Contains("创新", StringComparison.Ordinal),
                sentence => CountSignals(sentence, "创新", "融合", "改进", "优化", "动态", "机制", "方法"),
                usedSentences,
                minimumSignalScore: 1)
            : null;

        if (objective is null && architecture is null && method is null && result is null)
        {
            return null;
        }

        var clauses = new List<string>();
        if (objective is not null)
        {
            clauses.Add($"根据当前文档，这篇论文主要讲的是{objective.Sentence} {BuildStableCitation(objective.Chunk)}");
        }

        if (architecture is not null)
        {
            var architectureSentence = architecture.Sentence;
            if (ContainsAllSignals(architectureSentence, "感知", "决策", "执行", "交互"))
            {
                architectureSentence += "，对应数据采集、决策控制、执行机构和交互显示四个核心环节";
            }

            clauses.Add($"系统设计上还涉及{architectureSentence} {BuildStableCitation(architecture.Chunk)}");
        }

        if (method is not null && (wantsDetailedAnswer || architecture is null))
        {
            clauses.Add($"方法实现上关注{method.Sentence} {BuildStableCitation(method.Chunk)}");
        }

        if (result is not null)
        {
            clauses.Add($"效果结论上显示{result.Sentence} {BuildStableCitation(result.Chunk)}");
        }

        if (innovation is not null)
        {
            clauses.Add($"另外强调的创新点是{innovation.Sentence} {BuildStableCitation(innovation.Chunk)}");
        }

        return string.Join("。", clauses) + "。";
    }

    private string? BuildSummaryFallbackAnswer(IReadOnlyList<DocumentChunk> chunks, bool wantsDetailedAnswer)
    {
        var documentChunks = GetPrimaryDocumentChunks(chunks);
        if (documentChunks.Count == 0)
        {
            return null;
        }

        var candidates = new List<SummaryCandidate>();
        foreach (var chunk in documentChunks)
        {
            var chunkBias = GetSummaryChunkBias(chunk);
            var sentences = GetCleanSentences(chunk.Text, 8).ToArray();

            for (var index = 0; index < sentences.Length; index++)
            {
                var sentence = CleanStructuredSentence(sentences[index]);
                if (!IsCandidateSentence(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                if (!ContainsCjk(sentence) || sentence.Length < 14)
                {
                    continue;
                }

                var score = chunkBias;
                if (index == 0)
                {
                    score += 2;
                }

                score += CountSummarySignals(sentence) * 2;
                if (IsParameterHeavySentence(sentence))
                {
                    score -= 6;
                }

                score += CountArchitectureSignals(sentence);
                score += CountMethodSignals(sentence);
                score += CountProcedureStructureSignals(sentence);

                if (sentence.Contains("本文", StringComparison.Ordinal)
                    || sentence.Contains("文中", StringComparison.Ordinal)
                    || sentence.Contains("系统", StringComparison.Ordinal)
                    || sentence.Contains("研究", StringComparison.Ordinal)
                    || sentence.Contains("设计", StringComparison.Ordinal)
                    || sentence.Contains("实现", StringComparison.Ordinal))
                {
                    score += 2;
                }

                candidates.Add(new SummaryCandidate(chunk, sentence, score));
            }
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .DistinctBy(item => NormalizeSummarySentence(item.Sentence), StringComparer.Ordinal)
            .Take(wantsDetailedAnswer ? 3 : 2)
            .ToArray();

        if (selected.Length == 0)
        {
            return null;
        }

        var lead = selected[0];
        var builder = new StringBuilder();
        builder.Append($"根据当前文档，这份材料主要内容是{lead.Sentence} {BuildStableCitation(lead.Chunk)}。");

        if (selected.Length > 1)
        {
            var remainder = selected
                .Skip(1)
                .DistinctBy(item => NormalizeSummarySentence(item.Sentence), StringComparer.Ordinal)
                .Take(2)
                .ToArray();

            if (remainder.Length > 0)
            {
                foreach (var item in remainder)
                {
                    builder.Append($"另外，{CleanStructuredSentence(item.Sentence)} {BuildStableCitation(item.Chunk)}。");
                }
            }
        }

        return builder.ToString();
    }

    private IReadOnlyList<DocumentChunk> GetPrimaryDocumentChunks(IReadOnlyList<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return [];
        }

        var primaryFilePath = chunks
            .GroupBy(chunk => chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(primaryFilePath))
        {
            return chunks.ToArray();
        }

        var fromCorpus = _chunks
            .Where(chunk => string.Equals(chunk.FilePath, primaryFilePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (fromCorpus.Length > 0)
        {
            return fromCorpus;
        }

        return chunks
            .Where(chunk => string.Equals(chunk.FilePath, primaryFilePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private SummaryCandidate? SelectBestSummarySentence(
        IReadOnlyList<DocumentChunk> chunks,
        Func<DocumentChunk, bool> chunkFilter,
        Func<string, int> signalScorer,
        ISet<string>? excludedSentences = null,
        int minimumSignalScore = 1)
    {
        SummaryCandidate? best = null;
        foreach (var chunk in chunks.Where(chunkFilter))
        {
            var chunkBias = GetSummaryChunkBias(chunk);
            var sentences = GetCleanSentences(chunk.Text, 8);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                var normalizedSentence = NormalizeSummarySentence(sentence);
                if (excludedSentences?.Contains(normalizedSentence) == true)
                {
                    continue;
                }

                var signalScore = signalScorer(sentence);
                if (signalScore < minimumSignalScore)
                {
                    continue;
                }

                var score = chunkBias + CountSummarySignals(sentence) + (signalScore * 2);
                if (IsParameterHeavySentence(sentence))
                {
                    score -= 8;
                }

                if (score <= 0)
                {
                    continue;
                }

                var candidate = new SummaryCandidate(chunk, sentence, score);
                if (best is null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        if (best is not null)
        {
            excludedSentences?.Add(NormalizeSummarySentence(best.Sentence));
        }

        return best;
    }

    private static bool IsModuleQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("功能模块", StringComparison.Ordinal)
            || question.Contains("哪些模块", StringComparison.Ordinal)
            || question.Contains("模块有哪些", StringComparison.Ordinal)
            || question.Contains("主要模块", StringComparison.Ordinal);
    }

    private static bool IsModuleImplementationQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        if (!question.Contains("模块", StringComparison.Ordinal))
        {
            return false;
        }

        return question.Contains("怎么做", StringComparison.Ordinal)
            || question.Contains("怎么实现", StringComparison.Ordinal)
            || question.Contains("如何实现", StringComparison.Ordinal)
            || question.Contains("如何设计", StringComparison.Ordinal)
            || question.Contains("是怎么做的", StringComparison.Ordinal)
            || question.Contains("是如何实现的", StringComparison.Ordinal);
    }

    private static string ExtractModuleSubject(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        normalized = normalized
            .Replace("文档里", string.Empty, StringComparison.Ordinal)
            .Replace("文中", string.Empty, StringComparison.Ordinal)
            .Replace("这个", string.Empty, StringComparison.Ordinal)
            .Replace("该", string.Empty, StringComparison.Ordinal)
            .Trim();

        var match = Regex.Match(
            normalized,
            @"(?<subject>.+?模块)(?:是|具体|到底)?(?:怎么做|怎么实现|如何实现|如何设计|是怎么做的|是如何实现的).*$",
            RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["subject"].Value.Trim('，', '。', '？', '?', '！', '!', '：', ':', '；', ';', ' ');
        }

        var moduleIndex = normalized.IndexOf("模块", StringComparison.Ordinal);
        if (moduleIndex >= 0)
        {
            return normalized[..(moduleIndex + 2)].Trim();
        }

        return ExtractQuestionSubject(normalized) ?? string.Empty;
    }

    private static bool ChunkMatchesModuleSubject(DocumentChunk chunk, IReadOnlyList<string> focusTerms)
    {
        if (focusTerms.Count == 0)
        {
            return false;
        }

        var structureCorpus = $"{chunk.SectionTitle} {chunk.StructurePath}".ToLowerInvariant();
        var textCorpus = chunk.Text.ToLowerInvariant();
        var structureHits = focusTerms.Count(term => structureCorpus.Contains(term, StringComparison.Ordinal));
        var textHits = focusTerms.Count(term => textCorpus.Contains(term, StringComparison.Ordinal));
        return structureHits > 0 || textHits >= 2;
    }

    private string? BuildModuleFallbackAnswer(IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<DocumentChunk>? corpusChunks = null)
    {
        var moduleChunks = (corpusChunks ?? _chunks)
            .Where(chunk => chunk.SectionTitle.Contains("模块", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("模块", StringComparison.Ordinal)
                || chunk.Text.Contains("模块", StringComparison.Ordinal))
            .Select(chunk =>
            {
                var moduleName = ExtractModuleName(chunk);
                var normalizedName = NormalizeModuleName(moduleName);
                return new { chunk, moduleName, normalizedName };
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.moduleName))
            .DistinctBy(item => item.normalizedName, StringComparer.Ordinal)
            .OrderBy(item => GetModuleSortKey(item.moduleName), StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        if (moduleChunks.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，系统主要包含以下功能模块：");
        for (var index = 0; index < moduleChunks.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {moduleChunks[index].moduleName} {BuildStableCitation(moduleChunks[index].chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private string? BuildCompositionFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var slotAnswer = BuildCompositionSlotFallbackAnswer(question, chunks);
        if (!string.IsNullOrWhiteSpace(slotAnswer))
        {
            return slotAnswer;
        }

        var queryTokens = ExtractQueryTokens(question);
        var candidates = new List<ProcedureCandidate>();

        foreach (var chunk in chunks)
        {
            var structureBoost = CountCompositionStructureSignals($"{chunk.StructurePath} {chunk.SectionTitle}");
            var sentences = GetCleanSentences(chunk.Text, 10);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence))
                {
                    continue;
                }

                var cleanedSentence = CleanStructuredSentence(sentence);
                if (IsLikelyFragmentSentence(cleanedSentence))
                {
                    continue;
                }

                var lowerSentence = cleanedSentence.ToLowerInvariant();
                var tokenHits = queryTokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                var compositionHits = CompositionSignals.Count(signal => cleanedSentence.Contains(signal, StringComparison.Ordinal));
                var structureHits = CountCompositionStructureSignals(cleanedSentence);

                var score = (tokenHits * 3)
                    + (compositionHits * 2)
                    + structureBoost
                    + structureHits;

                if (chunk.ContentKind == "summary")
                {
                    score += 1;
                }
                else if (chunk.ContentKind == "body")
                {
                    score += 2;
                }

                if (IsJsonChunk(chunk))
                {
                    score += 2;
                }

                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ProcedureCandidate(chunk, cleanedSentence, score));
            }
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .GroupBy(item => $"{item.Chunk.FilePath}#{item.Chunk.Index}", StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(4)
            .ToArray();

        if (selected.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，相关组成、配置或条件信息主要包括：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Sentence} {BuildStableCitation(selected[index].Chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private string? BuildCompositionSlotFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        if (ContainsAny(question, "硬件", "设备", "部件", "器件", "元件"))
        {
            return BuildHardwareCompositionSlotFallbackAnswer(question, chunks);
        }

        var documentChunks = chunks
            .Where(chunk => chunk.ContentKind is "body" or "summary" or "")
            .ToArray();
        if (documentChunks.Any(IsJsonChunk))
        {
            documentChunks = documentChunks.Where(IsJsonChunk).ToArray();
        }
        if (documentChunks.Length == 0)
        {
            return null;
        }

        if (IsTechnologyStackQuestion(question) || ContainsAny(question, "组成", "构成", "配置", "条件", "架构", "模块"))
        {
            var entityAnswer = BuildCompositionEntityAnswer(question, documentChunks);
            if (!string.IsNullOrWhiteSpace(entityAnswer))
            {
                return entityAnswer;
            }
        }

        var queryTokens = ExtractQueryTokens(question)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        var usedSentences = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<(string Label, SlotCandidate Candidate)>();

        if (!ContainsAny(question, "技术栈", "架构", "前端", "后端", "软件", "组成", "配置", "系统"))
        {
            return null;
        }

        AddLabeledSlot(slots, "总体架构", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "架构", "前后端", "分层", "总体", "系统", "通信", "接口", "api") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "后端/服务", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "后端", "服务", "控制器", "接口", "框架", "api", "http", "模型调用") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "前端/交互", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "前端", "界面", "页面", "组件", "展示", "显示", "交互", "app", "路由") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "数据/存储", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "数据库", "存储", "持久化", "数据表", "实体表", "关系表", "缓存", "配置") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "可视化/输出", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "可视化", "图谱", "图表", "展示", "输出", "渲染", "分析", "检索") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "硬件/设备", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "硬件", "主控", "芯片", "传感器", "执行", "驱动", "显示屏", "通信模块", "电源") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));

        var selected = slots
            .Where(item => item.Candidate.Score >= 6)
            .GroupBy(item => NormalizeSummarySentence(item.Candidate.Sentence), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(5)
            .ToArray();
        if (selected.Length < 2)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，相关组成可以按这些部分理解：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Label}：{selected[index].Candidate.Sentence} {BuildStableCitation(selected[index].Candidate.Chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string? BuildCompositionEntityAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var entities = ExtractCompositionEntities(chunks)
            .ToArray();
        if (entities.Length < 3)
        {
            return null;
        }

        var groups = entities
            .GroupBy(entity => entity.Category, StringComparer.Ordinal)
            .Select(group => new
            {
                Category = group.Key,
                Items = group
                    .GroupBy(entity => entity.Text, StringComparer.OrdinalIgnoreCase)
                    .Select(entityGroup => entityGroup.OrderByDescending(entity => entity.Score).First())
                    .OrderByDescending(entity => entity.Score)
                    .ThenBy(entity => entity.Text, StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToArray()
            })
            .Where(group => group.Items.Length > 0)
            .OrderBy(group => GetCompositionCategoryOrder(group.Category))
            .ThenBy(group => group.Category, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var totalEntityCount = groups.Sum(group => group.Items.Length);
        if (groups.Length < 2 && totalEntityCount < 4)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(IsTechnologyStackQuestion(question)
            ? "根据当前文档，系统的总体架构和技术栈可以按这些层次归纳："
            : "根据当前文档，相关组成、配置或条件可以按这些层次归纳：");
        var written = 0;
        foreach (var group in groups)
        {
            written++;
            var terms = group.Items.Select(item => item.Text).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var representative = group.Items.OrderByDescending(item => item.Score).First().Chunk;
            builder.AppendLine($"{written}. {group.Category}：{string.Join("、", terms)} {BuildStableCitation(representative)}");
        }

        return written >= 2 ? builder.ToString().TrimEnd() : null;
    }

    private static bool IsTechnologyStackQuestion(string question)
    {
        return ContainsAny(question, TechnicalStackQuestionSignals)
            || question.Contains("采用了哪些技术", StringComparison.Ordinal)
            || question.Contains("采用什么技术", StringComparison.Ordinal);
    }

    private static IReadOnlyList<ExtractedEntity> ExtractCompositionEntities(IReadOnlyList<DocumentChunk> chunks)
    {
        var entities = new List<ExtractedEntity>();
        foreach (var chunk in chunks)
        {
            var context = $"{chunk.SectionTitle} {chunk.StructurePath}";
            var structureScore = CountCompositionStructureSignals(context);
            var sentences = GetCleanSentences(chunk.Text, 16)
                .Where(sentence => !IsMetadataLikeSentence(sentence) && !IsReferenceLikeSentence(sentence))
                .ToArray();

            foreach (var sentence in sentences)
            {
                var sentenceScore = CountCompositionStructureSignals(sentence)
                    + CountSystemModuleSignals(sentence)
                    + CountArchitectureSignals(sentence);
                if (structureScore + sentenceScore <= 0)
                {
                    continue;
                }

                foreach (var entityText in ExtractGenericAnswerEntities(sentence))
                {
                    var category = ClassifyCompositionEntity($"{context} {sentence}", entityText);
                    var score = 2 + structureScore + sentenceScore + CountSignals(entityText, "模块", "系统", "框架", "协议", "数据库", "接口", "算法", "芯片", "传感器");
                    if (chunk.ContentKind == "body")
                    {
                        score += 2;
                    }
                    else if (chunk.ContentKind == "summary")
                    {
                        score += 1;
                    }

                    entities.Add(new ExtractedEntity(entityText, category, chunk, score));
                }
            }

            if (entities.Count == 0 || IsJsonChunk(chunk))
            {
                foreach (var line in chunk.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var colonIndex = line.IndexOfAny([':', '：']);
                    if (colonIndex <= 0 || colonIndex >= line.Length - 1)
                    {
                        continue;
                    }

                    var value = CleanStructuredSentence(line[(colonIndex + 1)..]);
                    if (value.Length is < 2 or > 80)
                    {
                        continue;
                    }

                    foreach (var entityText in ExtractGenericAnswerEntities(value))
                    {
                        var category = ClassifyCompositionEntity($"{context} {line}", entityText);
                        entities.Add(new ExtractedEntity(entityText, category, chunk, 3 + structureScore));
                    }
                }
            }
        }

        return entities
            .Where(entity => IsUsefulExtractedEntity(entity.Text))
            .GroupBy(entity => $"{entity.Category}\0{entity.Text}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entity => entity.Score).First())
            .OrderByDescending(entity => entity.Score)
            .ToArray();
    }

    private static IEnumerable<string> ExtractGenericAnswerEntities(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in TechnicalEntityRegex.Matches(text))
        {
            var value = NormalizeExtractedEntity(match.Value);
            if (IsUsefulExtractedEntity(value))
            {
                yield return value;
            }
        }

        foreach (Match match in ChineseEntityRegex.Matches(text))
        {
            var value = NormalizeExtractedEntity(match.Value);
            if (IsUsefulExtractedEntity(value))
            {
                yield return value;
            }
        }
    }

    private static int CountGenericAnswerEntities(string text)
    {
        return ExtractGenericAnswerEntities(text).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private static string NormalizeExtractedEntity(string value)
    {
        var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
        normalized = normalized.Trim('，', '。', '；', ';', '、', ':', '：', '(', ')', '（', '）', '[', ']');
        return normalized;
    }

    private static bool IsUsefulExtractedEntity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeLookupText(value);
        if (normalized.Length < 2 || GenericEntityStopWords.Contains(value) || GenericEntityStopWords.Contains(normalized))
        {
            return false;
        }

        if (value.Length > 40)
        {
            return false;
        }

        if (value.All(char.IsDigit))
        {
            return false;
        }

        if (value.Count(character => character is >= '\u4e00' and <= '\u9fff') >= 12
            && CountSignals(value, "模块", "系统", "平台", "框架", "数据库", "接口", "协议", "算法", "控制器", "传感器", "芯片", "设备") == 0)
        {
            return false;
        }

        return true;
    }

    private static string ClassifyCompositionEntity(string context, string entity)
    {
        var source = $"{context} {entity}";
        if (ContainsAny(source, "后端", "服务端", "服务", "控制器", "api", "API", "接口服务"))
        {
            return "后端/服务";
        }

        if (ContainsAny(source, "前端", "客户端", "界面", "页面", "组件", "交互", "App", "APP", "移动端"))
        {
            return "前端/交互";
        }

        if (ContainsAny(source, "数据库", "存储", "持久化", "数据表", "缓存", "文件", "索引", "知识库"))
        {
            return "数据/存储";
        }

        if (ContainsAny(source, "可视化", "图表", "图谱", "展示", "渲染", "输出", "报表"))
        {
            return "可视化/输出";
        }

        if (ContainsAny(source, "硬件", "设备", "芯片", "处理器", "控制器", "传感器", "电源", "电机", "阀", "屏"))
        {
            return "硬件/设备";
        }

        if (ContainsAny(source, "通信", "联网", "协议", "串口", "总线", "接口", "上传", "下发", "传输"))
        {
            return "通信/接口";
        }

        if (ContainsAny(source, "算法", "模型", "规则", "策略", "控制", "识别", "抽取", "分析", "计算"))
        {
            return "算法/处理";
        }

        if (ContainsAny(source, "环境", "部署", "运行", "依赖", "配置", "条件", "参数"))
        {
            return "环境/配置";
        }

        if (ContainsAny(source, "架构", "分层", "层", "系统", "模块", "平台"))
        {
            return "总体架构";
        }

        return "组成项";
    }

    private static int GetCompositionCategoryOrder(string category)
    {
        return category switch
        {
            "总体架构" => 0,
            "硬件/设备" => 1,
            "后端/服务" => 2,
            "前端/交互" => 3,
            "数据/存储" => 4,
            "通信/接口" => 5,
            "算法/处理" => 6,
            "可视化/输出" => 7,
            "环境/配置" => 8,
            _ => 20
        };
    }

    private string? BuildHardwareCompositionSlotFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var documentChunks = chunks
            .Where(chunk => chunk.ContentKind is "body" or "summary" or "")
            .ToArray();
        if (documentChunks.Length == 0)
        {
            return null;
        }

        var queryTokens = ExtractQueryTokens(question)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        var usedSentences = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<(string Label, SlotCandidate Candidate)>();

        AddLabeledSlot(slots, "主控/处理", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "主控", "芯片", "控制器", "处理器", "mcu", "单片机", "核心") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "传感/采集", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "传感器", "采集", "检测", "测量", "湿度", "温度", "adc", "感知") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "执行/驱动", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "执行", "驱动", "阀", "电机", "继电器", "pwm", "输出", "控制") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "显示/交互", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "显示", "屏", "oled", "lcd", "界面", "按键", "交互", "状态") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "通信/联网", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "通信", "wifi", "蓝牙", "以太网", "串口", "无线", "联网", "模块", "协议") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));

        var selected = slots
            .Where(item => item.Candidate.Score >= 6)
            .GroupBy(item => NormalizeSummarySentence(item.Candidate.Sentence), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(5)
            .ToArray();
        if (selected.Length < 2)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，硬件组成可以按这些部分理解：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Label}：{selected[index].Candidate.Sentence} {BuildStableCitation(selected[index].Candidate.Chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AddLabeledSlot(List<(string Label, SlotCandidate Candidate)> slots, string label, SlotCandidate? candidate)
    {
        if (candidate is not null)
        {
            slots.Add((label, candidate));
        }
    }

    private string? BuildDefinitionFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var subject = ExtractQuestionSubject(question);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var primary = FindBestSubjectSentence(subject, chunks, DefinitionSignals, preferBodyContent: true);
        if (primary is null)
        {
            return null;
        }

        var secondary = FindBestSubjectSentence(subject, chunks, UsageSignals, preferBodyContent: true, excludedSentence: primary.Sentence);
        var clauses = new List<string>
        {
            $"根据当前文档，关于{subject}，{CleanStructuredSentence(primary.Sentence)} {BuildStableCitation(primary.Chunk)}"
        };

        if (secondary is not null)
        {
            clauses.Add($"进一步看它的作用，{CleanStructuredSentence(secondary.Sentence)} {BuildStableCitation(secondary.Chunk)}");
        }

        return string.Join("。", clauses) + "。";
    }

    private string? BuildModuleImplementationFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var subject = ExtractModuleSubject(question);
        var focusTerms = ExtractQueryTokens($"{subject} {question}")
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        if (focusTerms.Length == 0)
        {
            return null;
        }

        var slotAnswer = BuildModuleImplementationSlotAnswer(question, chunks, subject, focusTerms);
        if (!string.IsNullOrWhiteSpace(slotAnswer))
        {
            return slotAnswer;
        }

        var candidates = new List<ProcedureCandidate>();
        foreach (var chunk in chunks)
        {
            if (!ChunkMatchesModuleSubject(chunk, focusTerms))
            {
                continue;
            }

            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            var exactStructureMatch = !string.IsNullOrWhiteSpace(subject)
                && structureText.Contains(subject, StringComparison.OrdinalIgnoreCase);
            var structureFocusHits = focusTerms.Count(term => structureText.Contains(term, StringComparison.OrdinalIgnoreCase));
            var sentences = GetCleanSentences(chunk.Text, 10);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence))
                {
                    continue;
                }

                var lowerSentence = sentence.ToLowerInvariant();
                var tokenHits = focusTerms.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                var moduleHits = ModuleImplementationSignals.Count(signal => sentence.Contains(signal, StringComparison.OrdinalIgnoreCase));
                var methodHits = CountMethodSignals(sentence);
                var procedureHits = ProcedureSignals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
                var score = (tokenHits * 4)
                    + (moduleHits * 3)
                    + methodHits
                    + procedureHits
                    + structureFocusHits
                    + (exactStructureMatch ? 6 : 0);

                if (chunk.ContentKind == "summary")
                {
                    score -= 2;
                }

                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ProcedureCandidate(chunk, sentence, score));
            }
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .GroupBy(item => $"{item.Chunk.FilePath}#{item.Chunk.Index}", StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(4)
            .ToArray();

        if (selected.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        var lead = selected[0];
        builder.Append($"根据当前文档，这个模块的实现链路是：{lead.Sentence} {BuildStableCitation(lead.Chunk)}。");
        if (selected.Length > 1)
        {
            var tail = selected
                .Skip(1)
                .Select(item => item.Sentence)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();

            if (tail.Length > 0)
            {
                builder.Append($"进一步看，还包括{string.Join("，", tail)} {BuildStableCitation(selected[1].Chunk)}。");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private string? BuildCompareFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        if (!TryExtractComparisonSubjects(question, out var leftSubject, out var rightSubject))
        {
            return null;
        }

        var left = FindBestSubjectSentence(leftSubject, chunks, ProcedureSignals, preferBodyContent: true);
        var right = FindBestSubjectSentence(rightSubject, chunks, ProcedureSignals, preferBodyContent: true);
        if (left is null && right is null)
        {
            return null;
        }

        var clauses = new List<string>();
        if (left is not null)
        {
            clauses.Add($"{leftSubject}这边，{CleanStructuredSentence(left.Sentence)} {BuildStableCitation(left.Chunk)}");
        }

        if (right is not null)
        {
            clauses.Add($"{rightSubject}这边，{CleanStructuredSentence(right.Sentence)} {BuildStableCitation(right.Chunk)}");
        }

        return clauses.Count == 0
            ? null
            : $"根据当前文档，两者区别可以概括为：{string.Join("；", clauses)}。";
    }

    private string? BuildExplanationFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var queryTokens = ExtractQueryTokens(question);
        var candidates = new List<ProcedureCandidate>();

        foreach (var chunk in chunks)
        {
            var structureBoost = CountSignals($"{chunk.StructurePath} {chunk.SectionTitle}", "机制", "原理", "补偿", "策略", "原因");
            foreach (var sentence in GetCleanSentences(chunk.Text, 10))
            {
                if (!IsCandidateSentence(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                var lowerSentence = sentence.ToLowerInvariant();
                var tokenHits = queryTokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                var explanationHits = ExplanationSignals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
                var methodHits = CountMethodSignals(sentence);
                var score = (tokenHits * 3)
                    + (explanationHits * 3)
                    + methodHits
                    + structureBoost;

                if (chunk.ContentKind == "body")
                {
                    score += 2;
                }
                else if (chunk.ContentKind == "summary")
                {
                    score += 1;
                }

                if (score > 0)
                {
                    candidates.Add(new ProcedureCandidate(chunk, sentence, score));
                }
            }
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .GroupBy(item => NormalizeSummarySentence(item.Sentence), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(3)
            .ToArray();

        if (selected.Length == 0)
        {
            return null;
        }

        var lead = selected[0];
        var builder = new StringBuilder();
        builder.Append($"根据当前文档，原因/机制主要在于：{CleanStructuredSentence(lead.Sentence)} {BuildStableCitation(lead.Chunk)}。");

        if (selected.Length > 1)
        {
            var tail = selected
                .Skip(1)
                .Select(item => $"{CleanStructuredSentence(item.Sentence)} {BuildStableCitation(item.Chunk)}")
                .ToArray();
            builder.Append($"进一步看，{string.Join("；", tail)}。");
        }

        return builder.ToString().TrimEnd();
    }

    private string? BuildModuleImplementationSlotAnswer(string question, IReadOnlyList<DocumentChunk> chunks, string subject, IReadOnlyList<string> focusTerms)
    {
        var primaryChunks = GetPrimaryDocumentChunks(chunks)
            .Where(chunk => ChunkMatchesModuleSubject(chunk, focusTerms))
            .ToArray();
        if (primaryChunks.Length == 0)
        {
            return null;
        }

        var input = SelectBestSlotCandidate(
            primaryChunks,
            chunk => true,
            sentence => CountSignals(sentence, "输入", "接收", "采集", "获取", "读取", "数据", "状态", "参数", "请求", "事件"));
        var processing = SelectBestSlotCandidate(
            primaryChunks,
            chunk => true,
            sentence => CountSignals(sentence, "处理", "计算", "判断", "转换", "解析", "封装", "调用", "生成", "调度", "控制"));
        var interfaceOrTransport = SelectBestSlotCandidate(
            primaryChunks,
            chunk => true,
            sentence => CountSignals(sentence, "接口", "协议", "通信", "链路", "连接", "传输", "上传", "下发", "发送", "接收"));
        var output = SelectBestSlotCandidate(
            primaryChunks,
            chunk => true,
            sentence => CountSignals(sentence, "输出", "展示", "显示", "存储", "更新", "执行", "驱动", "反馈", "响应", "结果"));

        var slots = new List<SlotCandidate>();
        AddSlotCandidate(slots, input);
        AddSlotCandidate(slots, processing);
        AddSlotCandidate(slots, interfaceOrTransport);
        AddSlotCandidate(slots, output);

        if (slots.Count == 0)
        {
            return null;
        }

        var uniqueSlots = slots
            .GroupBy(item => NormalizeSummarySentence(item.Sentence), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var lead = string.IsNullOrWhiteSpace(subject) ? "这个模块" : subject;
        return $"根据当前文档，{lead}的实现链路是：{string.Join(" ", uniqueSlots.Select(item => $"{item.Sentence} {BuildStableCitation(item.Chunk)}"))}";
    }

    private static string NormalizeModuleName(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return string.Empty;
        }

        var normalized = LeadingOutlineRegex.Replace(moduleName.Trim(), string.Empty).Trim();
        normalized = normalized.Replace("第1项 ", string.Empty, StringComparison.Ordinal)
            .Replace("第2项 ", string.Empty, StringComparison.Ordinal)
            .Replace("第3项 ", string.Empty, StringComparison.Ordinal)
            .Replace("第4项 ", string.Empty, StringComparison.Ordinal)
            .Replace("第5项 ", string.Empty, StringComparison.Ordinal)
            .Replace("第6项 ", string.Empty, StringComparison.Ordinal)
            .Trim();
        return normalized;
    }

    private static string GetModuleSortKey(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return string.Empty;
        }

        var normalized = moduleName.Trim();
        var numberPrefix = Regex.Match(normalized, @"^\d+(?:\.\d+)*");
        if (numberPrefix.Success)
        {
            return numberPrefix.Value.PadLeft(8, '0') + normalized;
        }

        return "zzzzzzzz" + normalized;
    }

    private string? BuildPaperChallengeAndFutureFallbackAnswer(IReadOnlyList<DocumentChunk> chunks)
    {
        var challengeChunks = chunks
            .Where(chunk => chunk.SectionTitle.Contains("挑战", StringComparison.Ordinal)
                || chunk.SectionTitle.Contains("未来", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("挑战", StringComparison.Ordinal)
                || chunk.StructurePath.Contains("未来", StringComparison.Ordinal)
                || chunk.Text.Contains("挑战", StringComparison.Ordinal)
                || chunk.Text.Contains("未来", StringComparison.Ordinal))
            .Select(chunk => new
            {
                chunk,
                challenge = ExtractPaperChallengeSentence(chunk),
                future = ExtractPaperFutureSentence(chunk)
            })
            .ToArray();

        if (challengeChunks.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("当前问题涉及挑战与未来路径，文档给出的要点如下：");

        var itemIndex = 1;
        foreach (var item in challengeChunks)
        {
            if (!string.IsNullOrWhiteSpace(item.challenge))
            {
                builder.AppendLine($"{itemIndex}. {item.challenge} {BuildStableCitation(item.chunk)}");
                itemIndex++;
            }

            if (!string.IsNullOrWhiteSpace(item.future))
            {
                builder.AppendLine($"{itemIndex}. {item.future} {BuildStableCitation(item.chunk)}");
                itemIndex++;
            }
        }

        return itemIndex == 1 ? null : builder.ToString().TrimEnd();
    }

    private static string ExtractPaperChallengeSentence(DocumentChunk chunk)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        var candidates = SentenceSplitRegex.Split(source)
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Where(sentence => sentence.Contains("挑战", StringComparison.Ordinal)
                || sentence.Contains("问题", StringComparison.Ordinal)
                || sentence.Contains("局限", StringComparison.Ordinal)
                || sentence.Contains("不足", StringComparison.Ordinal)
                || sentence.Contains("同质化", StringComparison.Ordinal)
                || sentence.Contains("失真", StringComparison.Ordinal)
                || sentence.Contains("伦理", StringComparison.Ordinal))
            .ToArray();

        return candidates.FirstOrDefault() ?? string.Empty;
    }

    private static string ExtractPaperFutureSentence(DocumentChunk chunk)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        var candidates = SentenceSplitRegex.Split(source)
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Where(sentence => sentence.Contains("未来", StringComparison.Ordinal)
                || sentence.Contains("路径", StringComparison.Ordinal)
                || sentence.Contains("走向", StringComparison.Ordinal)
                || sentence.Contains("生态", StringComparison.Ordinal)
                || sentence.Contains("人机", StringComparison.Ordinal)
                || sentence.Contains("人文", StringComparison.Ordinal))
            .ToArray();

        return candidates.FirstOrDefault() ?? string.Empty;
    }

    private static string ExtractModuleName(DocumentChunk chunk)
    {
        var candidates = new[]
        {
            chunk.SectionTitle,
            GetLeafStructureSegment(chunk.StructurePath),
            chunk.Text
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var lines = candidate
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(3);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains("模块", StringComparison.Ordinal))
                {
                    continue;
                }

                var normalized = trimmed
                    .Replace("title:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("content:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (normalized.Length <= 64)
                {
                    return normalized;
                }
            }
        }

        return string.Empty;
    }

    private string? BuildProcedureFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var slotAnswer = BuildImplementationSlotFallbackAnswer(question, chunks);
        if (!string.IsNullOrWhiteSpace(slotAnswer))
        {
            return slotAnswer;
        }

        var queryTokens = ExtractQueryTokens(question);
        var candidates = new List<ProcedureCandidate>();

        foreach (var chunk in chunks)
        {
            var structureBoost = CountProcedureStructureSignals($"{chunk.StructurePath} {chunk.SectionTitle}");
            var chunkSignals = CountArchitectureSignals($"{chunk.StructurePath} {chunk.SectionTitle} {chunk.Text}");
            var sentences = GetCleanSentences(chunk.Text, 10);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence))
                {
                    continue;
                }

                var cleanedSentence = CleanStructuredSentence(sentence);
                if (IsLikelyFragmentSentence(cleanedSentence))
                {
                    continue;
                }

                var lowerSentence = cleanedSentence.ToLowerInvariant();
                var tokenHits = queryTokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                var procedureHits = ProcedureSignals.Count(signal => cleanedSentence.Contains(signal, StringComparison.Ordinal));
                var architectureHits = CountArchitectureSignals(cleanedSentence);
                var methodHits = CountMethodSignals(cleanedSentence);

                var score = (tokenHits * 3)
                    + (procedureHits * 2)
                    + architectureHits
                    + methodHits
                    + structureBoost
                    + chunkSignals;

                if (chunk.ContentKind == "summary")
                {
                    score += 1;
                }

                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ProcedureCandidate(chunk, cleanedSentence, score));
            }
        }

        var selected = candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .GroupBy(item => $"{item.Chunk.FilePath}#{item.Chunk.Index}", StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(4)
            .ToArray();

        if (selected.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，相关流程或实现要点包括：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Sentence} {BuildStableCitation(selected[index].Chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private string? BuildImplementationSlotFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        if (!IsProcedureQuestion(question) && !ContainsAny(question, "实现", "流程", "步骤", "过程", "机制", "方法"))
        {
            return null;
        }

        var documentChunks = chunks
            .Where(chunk => chunk.ContentKind is "body" or "summary" or "")
            .ToArray();
        if (documentChunks.Length == 0)
        {
            return null;
        }

        var queryTokens = ExtractQueryTokens(question)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        var usedSentences = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<(string Label, SlotCandidate Candidate)>();

        AddLabeledSlot(slots, "输入/前提", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "输入", "前提", "采集", "获取", "接收", "读取", "检测", "监测", "数据", "参数", "状态", "条件") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "核心处理/决策", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "处理", "计算", "判断", "决策", "规则", "算法", "模型", "策略", "控制", "生成", "解析") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "过程/步骤", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "首先", "然后", "再", "最后", "步骤", "流程", "阶段", "过程", "链路", "转换", "调用") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "输出/执行", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "输出", "执行", "驱动", "发送", "下发", "展示", "显示", "存储", "更新", "响应", "结果") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));
        AddExcludedSlotSentence(usedSentences, slots.LastOrDefault().Candidate);

        AddLabeledSlot(slots, "反馈/约束", SelectBestSlotCandidate(
            documentChunks,
            chunk => true,
            sentence => CountSignals(sentence, "反馈", "补偿", "校正", "优化", "异常", "阈值", "条件", "约束", "实时", "动态", "闭环") * 2
                + CountQueryTokenHits(sentence, queryTokens),
            usedSentences,
            preferBodyContent: true));

        var selected = slots
            .Where(item => item.Candidate.Score >= 6)
            .GroupBy(item => NormalizeSummarySentence(item.Candidate.Sentence), StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(5)
            .ToArray();
        if (selected.Length < 3)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("根据当前文档，具体实现链路可以按证据槽位理解：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Label}：{selected[index].Candidate.Sentence} {BuildStableCitation(selected[index].Candidate.Chunk)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool WantsDetailedAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return DetailSignals.Any(signal => question.Contains(signal, StringComparison.Ordinal))
            || question.Contains("详细一点", StringComparison.Ordinal)
            || question.Contains("具体一点", StringComparison.Ordinal)
            || question.Contains("详细介绍", StringComparison.Ordinal)
            || question.Contains("详细说明", StringComparison.Ordinal)
            || question.Contains("展开说明", StringComparison.Ordinal)
            || question.Contains("主要写什么", StringComparison.Ordinal)
            || question.Contains("主要内容", StringComparison.Ordinal)
            || question.Contains("主要讲", StringComparison.Ordinal)
            || question.Contains("从哪些方面", StringComparison.Ordinal);
    }

    private static bool IsSummaryQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("总结", StringComparison.Ordinal)
            || question.Contains("概述", StringComparison.Ordinal)
            || question.Contains("综述", StringComparison.Ordinal)
            || question.Contains("主要写什么", StringComparison.Ordinal)
            || question.Contains("主要写的是什么", StringComparison.Ordinal)
            || question.Contains("主要写的是啥", StringComparison.Ordinal)
            || question.Contains("主要内容", StringComparison.Ordinal)
            || question.Contains("主要讲", StringComparison.Ordinal)
            || question.Contains("论文主要写", StringComparison.Ordinal)
            || question.Contains("文章主要写", StringComparison.Ordinal)
            || question.Contains("文档主要写", StringComparison.Ordinal)
            || question.Contains("讲了什么", StringComparison.Ordinal)
            || question.Contains("说了什么", StringComparison.Ordinal);
    }

    private static bool ShouldAugmentWithPreviousQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var focusTerms = ExtractQueryTokens(question).Where(IsMeaningfulFocusTerm).ToArray();
        if (focusTerms.Length >= 2)
        {
            return false;
        }

        return WantsDetailedAnswer(question)
            || IsSummaryQuestion(question)
            || question.Contains("再说", StringComparison.Ordinal)
            || question.Contains("继续", StringComparison.Ordinal)
            || question.Contains("展开一点", StringComparison.Ordinal)
            || question.Contains("具体呢", StringComparison.Ordinal)
            || question.Contains("细一点", StringComparison.Ordinal);
    }

    private static string BuildAnswerStructureRule(QueryProfile queryProfile)
    {
        if (IsPaperQuestion(queryProfile.OriginalQuestion))
        {
            if (IsPaperChallengeAndFutureQuestion(queryProfile.OriginalQuestion))
            {
                return "如果问题同时在问“当前问题/挑战”和“未来路径/改进方向”，必须分成两部分回答：先列当前问题，再列未来路径或破解思路，每部分 2-4 点。";
            }

            if (IsPaperSummaryQuestion(queryProfile))
            {
                return queryProfile.WantsDetailedAnswer
                    ? "如果是在总结论文或文章，先用 1-2 句概括主题与核心结论，再按“研究对象/问题 -> 方法或系统设计 -> 关键发现或创新 -> 局限/启示”的主线自然展开；避免把相近内容拆成很多并列短句。"
                    : "如果是在总结论文或文章，先用 1 句概括主题与核心结论，再围绕一条主线自然展开 2-3 个层次；避免机械罗列多个并列要点。";
            }

            if (IsPaperContributionQuestion(queryProfile.OriginalQuestion))
            {
                return "如果是在问创新点、贡献或启示，先给总括判断，再分点列出 3-5 项，每点说明它具体体现在什么做法、观点或系统设计上。";
            }

            if (IsPaperLimitationQuestion(queryProfile.OriginalQuestion))
            {
                return "如果是在问局限、挑战、问题或未来方向，先给总括结论，再分点说明“现存问题/局限”以及“文中给出的改进方向或启示”。";
            }

            if (IsPaperSystemQuestion(queryProfile.OriginalQuestion))
            {
                return "如果是在问系统设计、架构、方法或模块，先说明总体架构，再分点写后端/前端/数据库/可视化或核心模块与关键流程，不要漏掉同一段中的成套技术栈。";
            }
        }

        if (queryProfile.Intent == "summary")
        {
            return queryProfile.WantsDetailedAnswer
                ? "如果是在总结文档，先概括主题与主旨，再围绕 2-3 个核心方面自然展开；优先保留关键模块名、方法名或阶段名，但不要把相近内容拆成很多碎片化短句。"
                : "如果是在总结文档，先概括主旨，再用 2-3 句或 2-3 个层次串起核心内容；不要只复述一句原文，也不要机械罗列多个并列短语。";
        }

        if (queryProfile.Intent == "metadata")
        {
            return "如果是在问标题、摘要、关键词等字段，按字段逐项回答；用户要求英文时，直接给出英文原文，不要翻译成中文，也不要混入其他无关段落。";
        }

        if (queryProfile.Intent == "composition")
        {
            return queryProfile.WantsDetailedAnswer
                ? "如果是在问组成、配置、条件或环境，先给整体判断，再按硬件/软件/模块/接口/环境等层次归并要点；原文有明确部件名、模块名、参数项或接口名时优先保留。"
                : "如果是在问组成、配置、条件或环境，先说整体，再列出关键组成项或约束条件；优先保留原文中的部件名、模块名、参数项或接口名。";
        }

        if (queryProfile.Intent == "compare")
        {
            return "如果是在做对比，先点明比较维度，再分别说明两边的特点与差异；不要只解释其中一边。";
        }

        if (queryProfile.Intent == "explain")
        {
            return "如果是在解释原因或机制，先给结论，再说明触发条件、作用机制和结果影响。";
        }

        if (queryProfile.Intent == "definition")
        {
            return "如果是在问概念或对象是什么，先给出定义或身份，再补充它的职责、特征或在系统中的位置。";
        }

        if (queryProfile.Intent == "module_implementation")
        {
            return queryProfile.WantsDetailedAnswer
                ? "如果是在问某个模块怎么做的，先点明该模块的目标和通信链路，再按“数据/状态从哪里来 -> 通过什么接口或协议传输 -> 在哪里显示或由谁下发控制 -> 系统如何执行或由哪一层完成”的顺序展开；不要泛化成整个系统流程。"
                : "如果是在问某个模块怎么做的，先说这个模块的实现链路，再列关键接口、协议、显示端和控制动作；不要答成整个系统的通用流程。";
        }

        if (queryProfile.Intent == "module_list")
        {
            return "如果是在问系统有哪些功能模块，先给总括，再列出核心模块名及各自职责；不要回答成技术栈、运行环境或单个模块的实现流程。";
        }

        if (queryProfile.WantsDetailedAnswer)
        {
            return "先给直接结论，再充分展开；按问题本身的结构来回答：流程题按步骤，解释题按原因或机制，模块/系统题按组成或层次，对比题按维度展开。若原文有明确模块名、算法名、协议名或字段名，优先沿用原词。";
        }

        return "先给直接结论，再用 2-4 句或 2-4 个要点展开依据；按问题类型保留必要的步骤、模块、技术栈或关键字段，不要只复述一句原文。若原文有明确模块名、算法名、协议名或字段名，优先沿用原词。";
    }

    private static string BuildCrossSourceSynthesisRule(QueryProfile queryProfile)
    {
        if (queryProfile.Intent == "summary")
        {
            return "如果多个来源能互相补充，要综合整理后回答，而不是逐条照抄；先归并同类信息，再提炼主线，不要把相近术语、模块或现象机械堆成一串名词。优先保留原文中的模块名、算法名、字段名、协议名、理论名、作品名。若原文已有固定术语，禁止自行改写成近义词。";
        }

        return "如果多个来源能互相补充，要综合整理后回答，而不是逐条照抄；但要按问题需要保留完整的步骤、模块、技术栈、字段或对比维度，不要为了“归并”而删掉关键并列项。优先保留原文中的模块名、算法名、字段名、协议名、理论名、作品名。若原文已有固定术语，禁止自行改写成近义词。";
    }

    private static string BuildDomainGuidance(QueryProfile queryProfile)
    {
        if (IsPaperQuestion(queryProfile.OriginalQuestion))
        {
            if (IsPaperChallengeAndFutureQuestion(queryProfile.OriginalQuestion))
            {
                return "这类论文问题必须同时覆盖“当前问题/挑战”和“未来路径/改进方向”，不能只答一半。";
            }

            if (IsPaperSystemQuestion(queryProfile.OriginalQuestion))
            {
                return "论文系统类问题优先保留原文中的技术栈、框架名、模块名、方法名和关键功能术语，并尽量按系统层次、组成模块、数据流程或功能模块分别展开。";
            }

            if (IsPaperContributionQuestion(queryProfile.OriginalQuestion))
            {
                return "论文观点类问题优先保留原文中的理论名、作品名、案例名、创新机制和关键术语，不要把具体观点泛化成空泛表述。";
            }

            if (IsPaperLimitationQuestion(queryProfile.OriginalQuestion))
            {
                return "论文反思类问题优先写清楚文中明确提出的局限、挑战、风险和改进路径，避免把外部常识混进来。";
            }
        }

        if (queryProfile.Intent == "summary")
        {
            if (queryProfile.WantsDetailedAnswer)
            {
                return "如果问题是在问论文或文档主要写什么，优先概括研究目标，再沿着“目标 -> 方法/系统 -> 结果/意义”自然展开；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，但不要把它们逐条堆成清单。";
            }

            return "如果问题是在问论文或文档主要写什么，先概括主旨，再用一条清晰主线串起核心内容点；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，但不要机械罗列。";
        }

        if (queryProfile.Intent == "metadata")
        {
            return "元数据类问题优先精确复用字段原文。若用户明确要求英文标题、英文摘要或英文关键词，直接输出对应英文字段值，并按标题/摘要/关键词分项给出。";
        }

        if (queryProfile.Intent == "composition")
        {
            return queryProfile.WantsDetailedAnswer
                ? "组成或配置类问题优先保留原文中的部件名、模块名、接口名、参数项和环境项，并按层次归并，不要混成流程描述。"
                : "组成或配置类问题优先点出关键组成项、配置项或条件项，不要把它回答成实现流程。";
        }

        if (queryProfile.Intent == "compare")
        {
            return "对比类问题优先同时保留两边的原始术语、阶段名或模块名，并明确差异点，不要只答单侧。";
        }

        if (queryProfile.Intent == "explain")
        {
            return "解释类问题优先保留原文中的因果词、机制词、条件词和结果词，例如由于、因此、为了、导致、影响、动态调节等。";
        }

        if (queryProfile.Intent == "definition")
        {
            return "定义类问题优先保留原文中的对象名、角色描述和职责描述，不要只给一个孤立名词。";
        }

        if (queryProfile.Intent == "module_implementation")
        {
            return queryProfile.WantsDetailedAnswer
                ? "模块实现类问题优先围绕模块本身写清接口、协议、上下行链路、显示端和控制动作；如原文给出了具体接口名、协议名、数据格式、终端或控制动作，应直接保留。"
                : "模块实现类问题优先点出接口、协议、显示/控制链路和执行动作，不要泛化成整个系统设计。";
        }

        if (queryProfile.Intent == "module_list")
        {
            return "模块清单类问题优先保留原文中的模块名，并概括每个模块的职责。";
        }

        if (queryProfile.Intent == "procedure")
        {
            return queryProfile.WantsDetailedAnswer
                ? "流程类问题优先按“输入或前提 -> 核心步骤 -> 输出或结果”展开；如果原文出现模块名、算法名、协议名或控制信号，尽量直接保留。"
                : "流程类问题优先点出前提、关键步骤和结果；如果原文出现模块名、算法名、协议名或控制信号，尽量直接保留。";
        }

        return queryProfile.WantsDetailedAnswer
            ? "回答时补足背景、关键细节和上下游关系，但不要脱离上下文扩写。"
            : "回答应紧扣问题，不要无关延伸。";
    }

    private static bool IsPaperQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("论文", StringComparison.Ordinal)
            || question.Contains("文章", StringComparison.Ordinal)
            || question.Contains("文中", StringComparison.Ordinal)
            || question.Contains("本文", StringComparison.Ordinal)
            || question.Contains("这篇文档", StringComparison.Ordinal)
            || question.Contains("这篇研究", StringComparison.Ordinal)
            || question.Contains("这篇", StringComparison.Ordinal);
    }

    private static bool IsPaperSummaryQuestion(QueryProfile queryProfile)
    {
        return queryProfile.Intent == "summary"
            || queryProfile.OriginalQuestion.Contains("主要写了什么", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("主要内容", StringComparison.Ordinal)
            || queryProfile.OriginalQuestion.Contains("讲了什么", StringComparison.Ordinal);
    }

    private static bool IsPaperContributionQuestion(string question)
    {
        return question.Contains("创新点", StringComparison.Ordinal)
            || question.Contains("创新", StringComparison.Ordinal)
            || question.Contains("贡献", StringComparison.Ordinal)
            || question.Contains("启示", StringComparison.Ordinal)
            || question.Contains("作用", StringComparison.Ordinal);
    }

    private static bool IsPaperLimitationQuestion(string question)
    {
        return question.Contains("局限", StringComparison.Ordinal)
            || question.Contains("不足", StringComparison.Ordinal)
            || question.Contains("挑战", StringComparison.Ordinal)
            || question.Contains("问题", StringComparison.Ordinal)
            || question.Contains("反思", StringComparison.Ordinal)
            || question.Contains("未来", StringComparison.Ordinal);
    }

    private static bool IsPaperChallengeAndFutureQuestion(string question)
    {
        return (question.Contains("问题", StringComparison.Ordinal)
                || question.Contains("挑战", StringComparison.Ordinal)
                || question.Contains("局限", StringComparison.Ordinal)
                || question.Contains("不足", StringComparison.Ordinal))
            && (question.Contains("未来", StringComparison.Ordinal)
                || question.Contains("路径", StringComparison.Ordinal)
                || question.Contains("改进", StringComparison.Ordinal)
                || question.Contains("走向", StringComparison.Ordinal));
    }

    private static bool IsPaperSystemQuestion(string question)
    {
        return question.Contains("系统", StringComparison.Ordinal)
            || question.Contains("架构", StringComparison.Ordinal)
            || question.Contains("技术栈", StringComparison.Ordinal)
            || question.Contains("模块", StringComparison.Ordinal)
            || question.Contains("方法", StringComparison.Ordinal)
            || question.Contains("实现", StringComparison.Ordinal)
            || question.Contains("流程", StringComparison.Ordinal);
    }

    private static bool IsCandidateSentence(string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Length < 12)
        {
            return false;
        }

        if (HtmlTagRegex.IsMatch(trimmed))
        {
            return false;
        }

        if (HeadingLineRegex.IsMatch(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("参考文献", StringComparison.Ordinal)
            || trimmed.StartsWith("关键词", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("key words", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsTitleOrAuthorLikeLine(trimmed))
        {
            return false;
        }

        return !IsReferenceLikeSentence(trimmed);
    }

    private static bool IsLikelyFragmentSentence(string sentence)
    {
        var trimmed = sentence.Trim();
        if (trimmed.Length < 12)
        {
            return true;
        }

        if (trimmed.Count(character => character is '，' or ',' or '、') >= 5
            && CountSignals(trimmed, "包括", "包含", "由", "组成", "构成") == 0)
        {
            return true;
        }

        if (!ContainsCjk(trimmed))
        {
            return false;
        }

        var hasPredicateSignal = CountSignals(trimmed, "包括", "包含", "采用", "选用", "负责", "实现", "用于", "连接", "采集", "控制", "显示", "上传", "下发", "组成", "构成") > 0;
        var hasTerminalPunctuation = trimmed.EndsWith('。') || trimmed.EndsWith('！') || trimmed.EndsWith('？') || trimmed.EndsWith('.');
        return !hasPredicateSignal && !hasTerminalPunctuation && trimmed.Length < 24;
    }

    private static bool IsUsageQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        return question.Contains("作用", StringComparison.Ordinal)
            || question.Contains("用途", StringComparison.Ordinal)
            || question.Contains("功能", StringComparison.Ordinal)
            || question.Contains("有什么用", StringComparison.Ordinal);
    }

    private static bool TryExtractComparisonSubjects(string question, out string leftSubject, out string rightSubject)
    {
        leftSubject = string.Empty;
        rightSubject = string.Empty;
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        normalized = normalized
            .Replace("请比较", string.Empty, StringComparison.Ordinal)
            .Replace("比较一下", string.Empty, StringComparison.Ordinal)
            .Replace("对比一下", string.Empty, StringComparison.Ordinal)
            .Trim();

        var match = Regex.Match(
            normalized,
            @"(?<left>.+?)(?:和|与|跟)(?<right>.+?)(?:之间)?(?:有何|有什么|有哪些)?(?:区别|不同|差异|对比).*$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        leftSubject = CleanComparisonSubject(match.Groups["left"].Value);
        rightSubject = CleanComparisonSubject(match.Groups["right"].Value);
        return !string.IsNullOrWhiteSpace(leftSubject) && !string.IsNullOrWhiteSpace(rightSubject);
    }

    private static string CleanComparisonSubject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim()
            .Trim('，', '。', '？', '?', '！', '!', '：', ':', '；', ';', ' ')
            .Replace("这篇论文里的", string.Empty, StringComparison.Ordinal)
            .Replace("文档里的", string.Empty, StringComparison.Ordinal)
            .Replace("文中", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeQuestionForSubjectExtraction(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        normalized = normalized
            .Replace("请问", string.Empty, StringComparison.Ordinal)
            .Replace("麻烦问一下", string.Empty, StringComparison.Ordinal)
            .Replace("我想问一下", string.Empty, StringComparison.Ordinal)
            .Replace("想请教一下", string.Empty, StringComparison.Ordinal)
            .Trim();

        var patterns = new[]
        {
            "在.*?中起什么作用.*$",
            "在.*?中有什么作用.*$",
            "在.*?中有什么用.*$",
            "起什么作用.*$",
            "有什么作用.*$",
            "有什么用途.*$",
            "有什么功能.*$",
            "有什么用.*$",
            "作用是什么.*$",
            "用途是什么.*$",
            "功能是什么.*$"
        };

        foreach (var pattern in patterns)
        {
            normalized = Regex.Replace(normalized, pattern, string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        normalized = normalized.Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ');
        return normalized;
    }

    private static string? ExtractQuestionSubject(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        if (TryExtractUsageQuestionSubject(question, out var usageSubject))
        {
            return usageSubject;
        }

        if (TryExtractDirectQuestionSubject(question, out var directSubject))
        {
            return directSubject;
        }

        var normalized = NormalizeQuestionForSubjectExtraction(question);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = BuildFocusTermSourceText(question).Trim();
        }

        var matches = QueryTokenRegex.Matches(normalized.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length >= 2)
            .Where(token => token is not "什么" and not "作用" and not "用途" and not "功能" and not "有什么" and not "一下" and not "请问" and not "详细一点" and not "具体一点")
            .Where(token => !token.Contains("作用", StringComparison.Ordinal))
            .Where(token => !token.Contains("用途", StringComparison.Ordinal))
            .Where(token => !token.Contains("功能", StringComparison.Ordinal))
            .Where(token => !token.Contains("补充要求", StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        var exactEntity = matches
            .Where(token => !ContainsCjk(token) || token.Length <= 12)
            .OrderByDescending(token => token.Any(char.IsAsciiLetterOrDigit))
            .ThenByDescending(token => token.Length)
            .FirstOrDefault();

        return exactEntity ?? matches.OrderByDescending(token => token.Length).First();
    }

    private static bool TryExtractDirectQuestionSubject(string question, out string subject)
    {
        subject = string.Empty;
        var normalized = NormalizeQuestionForSubjectExtraction(question);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var patterns = new[]
        {
            @"(?:里|中|内)?(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})(?:是指什么|指的是什么|指什么|是什么|定义是什么)",
            @"(?:为什么|为何)(?:需要|要|使用|采用|引入|配置)?(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})",
            @"(?<subject>[\u4e00-\u9fffA-Za-z0-9_\-+/\.]{2,32})(?:包括哪些|包含哪些|由哪些|有哪些部分|有哪些组成|怎么做|怎么实现|如何实现|如何设计)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var candidate = CleanExtractedQuestionSubject(match.Groups["subject"].Value);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                subject = candidate;
                return true;
            }
        }

        return false;
    }

    private static string CleanExtractedQuestionSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        var cleaned = subject.Trim()
            .Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ')
            .Replace("文档里", string.Empty, StringComparison.Ordinal)
            .Replace("文档中", string.Empty, StringComparison.Ordinal)
            .Replace("文中", string.Empty, StringComparison.Ordinal)
            .Replace("这篇论文里", string.Empty, StringComparison.Ordinal)
            .Replace("这篇文章里", string.Empty, StringComparison.Ordinal)
            .Replace("这份材料里", string.Empty, StringComparison.Ordinal)
            .Trim();

        var markers = new[] { "里", "中", "内", "的" };
        foreach (var marker in markers)
        {
            var markerIndex = cleaned.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0 && markerIndex < cleaned.Length - marker.Length)
            {
                cleaned = cleaned[(markerIndex + marker.Length)..].Trim();
            }
        }

        if (FocusStopWords.Contains(cleaned) || IsIntentExpansionOnlyToken(cleaned))
        {
            return string.Empty;
        }

        cleaned = Regex.Replace(cleaned, "(?:是|为|指|指的|定义)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        if (cleaned.Length < 2 || FocusStopWords.Contains(cleaned) || IsIntentExpansionOnlyToken(cleaned))
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static IReadOnlyList<string> ExtractQuestionSubjectTerms(string question)
    {
        var subject = ExtractQuestionSubject(question);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return [];
        }

        var terms = new List<string> { subject.ToLowerInvariant() };
        terms.AddRange(ExtractQueryTokens(subject)
            .Where(token => !IsIntentExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm));

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryExtractUsageQuestionSubject(string question, out string subject)
    {
        subject = string.Empty;
        if (!IsUsageQuestion(question))
        {
            return false;
        }

        var normalized = StripQuestionBoilerplate(BuildFocusTermSourceText(question).Trim());
        var match = UsageSubjectRegex.Match(normalized);
        if (!match.Success)
        {
            return false;
        }

        subject = CleanExtractedUsageSubject(match.Groups["subject"].Value);
        return !string.IsNullOrWhiteSpace(subject);
    }

    private static string CleanExtractedUsageSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        var cleaned = subject.Trim()
            .Trim('，', '。', '？', '?', '！', '!', '；', ';', '：', ':', '、', ' ')
            .Replace("文档里", string.Empty, StringComparison.Ordinal)
            .Replace("文档中", string.Empty, StringComparison.Ordinal)
            .Replace("文中", string.Empty, StringComparison.Ordinal)
            .Replace("里面", string.Empty, StringComparison.Ordinal)
            .Replace("其中", string.Empty, StringComparison.Ordinal)
            .Trim();

        var markers = new[] { "里", "中", "内", "的" };
        foreach (var marker in markers)
        {
            var markerIndex = cleaned.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0 && markerIndex < cleaned.Length - marker.Length)
            {
                cleaned = cleaned[(markerIndex + marker.Length)..].Trim();
            }
        }

        return cleaned;
    }

    private SlotCandidate? FindBestSubjectSentence(
        string subject,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> preferredSignals,
        bool preferBodyContent = false,
        string? excludedSentence = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var focusTerms = ExtractQueryTokens(subject)
            .Where(IsMeaningfulFocusTerm)
            .ToArray();
        if (focusTerms.Length == 0)
        {
            focusTerms = [subject.ToLowerInvariant()];
        }

        SlotCandidate? best = null;
        foreach (var chunk in chunks)
        {
            if (!ChunkMatchesSubject(chunk, subject))
            {
                continue;
            }

            var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
            var structureHits = focusTerms.Count(term => structureText.Contains(term, StringComparison.OrdinalIgnoreCase))
                + (structureText.Contains(subject, StringComparison.OrdinalIgnoreCase) ? 3 : 0);

            foreach (var sentence in GetCleanSentences(chunk.Text, 10))
            {
                if (!IsCandidateSentence(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(excludedSentence)
                    && NormalizeSummarySentence(sentence) == NormalizeSummarySentence(excludedSentence))
                {
                    continue;
                }

                var lowerSentence = sentence.ToLowerInvariant();
                var tokenHits = focusTerms.Count(term => lowerSentence.Contains(term, StringComparison.Ordinal));
                var signalHits = preferredSignals.Count(signal => sentence.Contains(signal, StringComparison.OrdinalIgnoreCase));
                var score = (tokenHits * 4)
                    + (signalHits * 3)
                    + structureHits
                    + CountMethodSignals(sentence);

                if (chunk.ContentKind == "summary")
                {
                    score += 1;
                }

                if (preferBodyContent)
                {
                    if (chunk.ContentKind == "body")
                    {
                        score += 3;
                    }
                    else if (chunk.ContentKind == "summary")
                    {
                        score -= 1;
                    }
                }

                if (best is null || score > best.Score)
                {
                    best = new SlotCandidate(chunk, sentence, score);
                }
            }
        }

        return best;
    }

    private SlotCandidate? SelectBestSlotCandidate(
        IEnumerable<DocumentChunk> chunks,
        Func<DocumentChunk, bool> chunkFilter,
        Func<string, int> signalScorer,
        ISet<string>? excludedNormalizedSentences = null,
        bool preferBodyContent = false)
    {
        SlotCandidate? best = null;
        foreach (var chunk in chunks.Where(chunkFilter))
        {
            foreach (var sentence in GetCleanSentences(chunk.Text, 10))
            {
                if (!IsCandidateSentence(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                var normalizedSentence = NormalizeSummarySentence(sentence);
                if (excludedNormalizedSentences?.Contains(normalizedSentence) == true)
                {
                    continue;
                }

                var signalScore = signalScorer(sentence);
                if (signalScore <= 0)
                {
                    continue;
                }

                var score = (signalScore * 3)
                    + CountMethodSignals(sentence)
                    + CountArchitectureSignals(sentence);

                if (chunk.ContentKind == "summary")
                {
                    score -= 2;
                }

                if (preferBodyContent)
                {
                    if (chunk.ContentKind == "body")
                    {
                        score += 4;
                    }
                    else if (chunk.ContentKind == "summary")
                    {
                        score -= 3;
                    }
                }

                if (IsParameterHeavySentence(sentence))
                {
                    score -= 2;
                }

                if (best is null || score > best.Score)
                {
                    best = new SlotCandidate(chunk, sentence, score);
                }
            }
        }

        return best;
    }

    private static void AddExcludedSlotSentence(ISet<string> excludedNormalizedSentences, SlotCandidate? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        excludedNormalizedSentences.Add(NormalizeSummarySentence(candidate.Sentence));
    }

    private static void AddSlotCandidate(List<SlotCandidate> slots, SlotCandidate? candidate)
    {
        if (candidate is not null)
        {
            slots.Add(candidate);
        }
    }

    private string? BuildUsageFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<DocumentChunk>? corpusChunks = null)
    {
        var subject = ExtractQuestionSubject(question);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var topSentences = CollectUsageCandidates(subject, chunks);
        if (topSentences.Count == 0)
        {
            topSentences = CollectUsageCandidates(subject, corpusChunks ?? _chunks);
        }

        if (topSentences.Count == 0)
        {
            return "我不知道（当前文档未覆盖）。";
        }

        var selected = topSentences
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ChunkIndex)
            .Take(3)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"根据文档，{subject} 的主要作用如下：");
        for (var i = 0; i < selected.Length; i++)
        {
            builder.AppendLine($"{i + 1}. {selected[i].Sentence} {BuildStableCitation(selected[i].FilePath, selected[i].SectionTitle, selected[i].ChunkIndex)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static List<UsageCandidate> CollectUsageCandidates(string subject, IReadOnlyList<DocumentChunk> chunks)
    {
        var candidates = new List<UsageCandidate>();
        foreach (var chunk in chunks)
        {
            var chunkMatchesSubject = ChunkMatchesSubject(chunk, subject);
            if (!chunkMatchesSubject)
            {
                continue;
            }

            var parts = GetCleanSentences(chunk.Text, 12);

            foreach (var sentence in parts)
            {
                if (IsReferenceLikeSentence(sentence) || IsTitleOrAuthorLikeLine(sentence) || IsMetadataLikeSentence(sentence))
                {
                    continue;
                }

                if (!ContainsCjk(sentence) || sentence.Length < 10)
                {
                    continue;
                }

                var lower = sentence.ToLowerInvariant();
                var directSubjectHit = lower.Contains(subject, StringComparison.Ordinal);
                var usageSignalHits = UsageSignals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
                var architectureSignalHits = CountArchitectureSignals(sentence);

                if (!directSubjectHit && usageSignalHits == 0 && architectureSignalHits == 0)
                {
                    continue;
                }

                var score = 0;
                if (directSubjectHit)
                {
                    score += 3;
                }

                if (CountSignals(chunk.SectionTitle, "核心", "控制", "功能", "实现", "模块", "服务", "接口") > 0)
                {
                    score += 2;
                }

                score += usageSignalHits * 2;
                score += architectureSignalHits;

                candidates.Add(new UsageCandidate(sentence, chunk.FilePath, chunk.SectionTitle, chunk.Index, score));
            }
        }

        return candidates;
    }

    private static bool ChunkMatchesSubject(DocumentChunk chunk, string subject)
    {
        var lowerSubject = subject.ToLowerInvariant();
        return chunk.Text.ToLowerInvariant().Contains(lowerSubject, StringComparison.Ordinal)
            || chunk.DocumentTitle.ToLowerInvariant().Contains(lowerSubject, StringComparison.Ordinal)
            || chunk.SectionTitle.ToLowerInvariant().Contains(lowerSubject, StringComparison.Ordinal)
            || chunk.StructurePath.ToLowerInvariant().Contains(lowerSubject, StringComparison.Ordinal);
    }

    private static int CountArchitectureSignals(string sentence)
    {
        var signals = new[] { "核心", "控制", "采集", "执行", "显示", "通信", "调度", "模块", "架构", "层", "接口", "数据", "状态", "参数" };
        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
    }

    private static bool ContainsAllSignals(string text, params string[] signals)
    {
        return signals.All(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    private static int CountSystemModuleSignals(string sentence)
    {
        var signals = new[]
        {
            "功能模块", "模块", "子系统", "功能", "组件", "组成", "接口", "服务",
            "管理", "展示", "分析", "处理", "检索", "抽取", "控制", "可视化"
        };

        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
    }

    private static bool IsIntentExpansionOnlyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        return DefinitionSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ExplanationSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || CompareSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || CompositionSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ProcedureSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || UsageSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || ModuleImplementationSignals.Contains(token, StringComparer.OrdinalIgnoreCase)
            || token is "原因" or "机制" or "原理" or "定义" or "方法" or "步骤" or "流程" or "架构";
    }

    private static bool HasAnyTermInChunks(IReadOnlyList<string> terms, IReadOnlyList<DocumentChunk> chunks)
    {
        if (terms.Count == 0 || chunks.Count == 0)
        {
            return false;
        }

        return chunks.Any(chunk =>
        {
            var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
            return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static int CountCompositionStructureSignals(string text)
    {
        var signals = new[] { "组成", "构成", "包括", "包含", "模块", "架构", "配置", "条件", "环境", "硬件", "软件", "接口", "依赖", "实验环境", "运行环境", "部署环境" };
        return signals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    private static int CountProcedureStructureSignals(string text)
    {
        var signals = new[] { "感知", "决策", "执行", "交互", "流程", "步骤", "控制", "调节", "算法", "模块", "策略" };
        return signals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    private static int CountMethodSignals(string sentence)
    {
        var signals = new[] { "采用", "通过", "结合", "根据", "实时", "动态", "驱动", "调节", "控制", "监测", "反馈" };
        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
    }

    private static int CountSignals(string sentence, params string[] signals)
    {
        return signals.Count(signal => sentence.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountQueryTokenHits(string sentence, IReadOnlyList<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var lowerSentence = sentence.ToLowerInvariant();
        return queryTokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
    }

    private static bool ContainsAnySignal(IEnumerable<DocumentChunk> chunks, params string[] signals)
    {
        return chunks.Any(chunk =>
        {
            var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
            return signals.Any(signal => source.Contains(signal, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static DocumentChunk? FindRepresentativeChunk(IReadOnlyList<DocumentChunk> chunks, params string[] signals)
    {
        return chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = CountSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}", signals)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Chunk.ContentKind == "body")
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .FirstOrDefault();
    }

    private static bool ContainsAny(string text, params string[] signals)
    {
        return signals.Any(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    private static bool IsCompositionQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        if (ContainsAny(question, CompositionQuestionSignals))
        {
            return true;
        }

        return (question.Contains("怎么样", StringComparison.Ordinal) || question.Contains("怎样的", StringComparison.Ordinal))
            && ContainsAny(question, CompositionContextSignals)
            && !ContainsAny(question, NonCompositionAttributeSignals);
    }

    private static bool IsProcedureQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        if (ContainsAny(question, ProcedureQuestionSignals))
        {
            return true;
        }

        return question.Contains("怎么", StringComparison.Ordinal)
            && !question.Contains("怎么样", StringComparison.Ordinal)
            && !question.Contains("怎样的", StringComparison.Ordinal);
    }

    private static bool ShouldUseDirectExplanationFallback(QueryProfile queryProfile, IReadOnlyList<DocumentChunk> chunks)
    {
        if (queryProfile.Intent != "explain")
        {
            return false;
        }

        var focusHits = queryProfile.FocusTerms.Count(term =>
            chunks.Any(chunk => BuildChunkRetrievalText(chunk).Contains(term, StringComparison.OrdinalIgnoreCase)));
        var mechanismHits = chunks.Count(chunk =>
            CountSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}", "机制", "原理", "原因", "影响", "导致", "由于", "因此", "反馈", "优化", "调节", "校正") > 0);

        return mechanismHits >= 1 && (focusHits >= 1 || queryProfile.FocusTerms.Count == 0);
    }

    private static int CountSummarySignals(string sentence)
    {
        var signals = new[]
        {
            "围绕", "面向", "针对", "提出", "设计", "实现", "构建", "搭建", "分析", "总结",
            "介绍", "包括", "主要", "核心", "模块", "架构", "方法", "流程", "结果", "结论"
        };

        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
    }

    private static int GetSummaryChunkBias(DocumentChunk chunk)
    {
        var score = 0;

        if (chunk.ContentKind == "summary")
        {
            score += 6;
        }

        if (chunk.ContentKind == "body")
        {
            score += 1;
        }

        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        var preferredSignals = new[]
        {
            "摘要", "abstract", "overview", "architecture", "workflow", "概述", "总结", "引言", "简介",
            "系统", "总体设计", "架构", "设计", "方法", "实现", "结果", "结论", "模块", "测试", "评估", "创新"
        };

        score += preferredSignals.Count(signal => structureText.Contains(signal, StringComparison.OrdinalIgnoreCase));
        if (IsParameterHeavySentence(chunk.Text))
        {
            score -= 4;
        }

        return score;
    }

    private static string NormalizeSummarySentence(string sentence)
    {
        return Regex.Replace(CleanStructuredSentence(sentence), "\\s+", string.Empty);
    }

    private static IEnumerable<string> GetCleanSentences(string text, int takeCount)
    {
        return SentenceSplitRegex.Split(text)
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(takeCount);
    }

    private static string CleanStructuredSentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence))
        {
            return string.Empty;
        }

        var cleaned = sentence.Trim();
        cleaned = Regex.Replace(cleaned, "^(?:content|overview|summary|abstract|architecture|workflow)\\s*[:：]\\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, "\\s+", " ");
        return cleaned.Trim();
    }

    private static bool IsParameterHeavySentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence))
        {
            return false;
        }

        var digitCount = sentence.Count(char.IsDigit);
        var lower = sentence.ToLowerInvariant();
        var parameterSignals = new[]
        {
            "mhz", "kb", "sram", "flash", "adc", "i²c", "spi", "usart", "dma", "μa", "ma",
            "v", "fps", "bps", "ms", "l/min", "cortex", "risc", "flash", "oled", "wifi"
        };

        var signalHits = parameterSignals.Count(signal => lower.Contains(signal, StringComparison.Ordinal));
        return digitCount >= 6 || (digitCount >= 3 && signalHits >= 2);
    }

    private static bool IsReferenceLikeSentence(string sentence)
    {
        var trimmed = sentence.Trim();
        if (ReferenceLineRegex.IsMatch(trimmed))
        {
            return true;
        }

        var hasJournalMarker = trimmed.Contains("[J]", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("doi", StringComparison.OrdinalIgnoreCase);

        var hasReferencePunctuation = trimmed.Contains("，", StringComparison.Ordinal)
            && trimmed.Contains(".", StringComparison.Ordinal)
            && (trimmed.Contains("(", StringComparison.Ordinal) || trimmed.Contains("：", StringComparison.Ordinal));

        var startsLikeReferenceNumber = trimmed.StartsWith("[", StringComparison.Ordinal) || char.IsDigit(trimmed[0]);
        return startsLikeReferenceNumber && hasJournalMarker && hasReferencePunctuation;
    }

    private static bool IsMetadataLikeSentence(string sentence)
    {
        var trimmed = sentence.Trim();
        if (MetadataLineRegex.IsMatch(trimmed))
        {
            return true;
        }

        return trimmed.Contains("关键词", StringComparison.Ordinal)
            || trimmed.Contains("中图分类号", StringComparison.Ordinal)
            || trimmed.Contains("文献标识码", StringComparison.Ordinal)
            || trimmed.Contains("文章编号", StringComparison.Ordinal)
            || trimmed.Contains("Key words", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("Abstract:", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeChunkForPrompt(string text)
    {
        var lines = text
            .Split('\n', StringSplitOptions.None)
            .Select(CleanStructuredSentence)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !HtmlTagRegex.IsMatch(line))
            .Where(line => !HeadingLineRegex.IsMatch(line))
            .Where(line => !IsMetadataLikeSentence(line))
            .Where(line => !IsReferenceLikeSentence(line))
            .Where(line => !IsTitleOrAuthorLikeLine(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return text;
        }

        return string.Join("\n", lines);
    }

    private static IReadOnlyList<DocumentChunk> FilterChunksByTargetFiles(IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths)
    {
        if (targetFilePaths.Count == 0)
        {
            return chunks;
        }

        var targetFilePathSet = new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var filtered = chunks
            .Where(chunk => targetFilePathSet.Contains(chunk.FilePath))
            .ToArray();

        return filtered.Length > 0 ? filtered : chunks;
    }

    private static bool HasTitleOrAuthorBlock(string answer)
    {
        var lines = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return true;
        }

        var suspicious = lines.Count(IsTitleOrAuthorLikeLine);
        return suspicious >= 2;
    }

    private static bool IsTitleOrAuthorLikeLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (HeadingLineRegex.IsMatch(trimmed) || HtmlTagRegex.IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.Contains("design of", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("based on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Contains("；", StringComparison.Ordinal)
            && trimmed.Any(char.IsUpper)
            && !trimmed.Contains("。", StringComparison.Ordinal)
            && !trimmed.Contains("，", StringComparison.Ordinal))
        {
            return true;
        }

        var hasChinese = ContainsCjk(trimmed);
        var hasEnglish = trimmed.Any(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        var hasSentencePunctuation = trimmed.Contains('。') || trimmed.Contains('，') || trimmed.Contains('：') || trimmed.Contains(':');

        if (!hasSentencePunctuation && hasChinese && hasEnglish && trimmed.Length > 24)
        {
            return true;
        }

        return false;
    }

    private static string BuildStableCitation(DocumentChunk chunk)
    {
        return BuildStableCitation(chunk.FilePath, string.IsNullOrWhiteSpace(chunk.StructurePath) ? chunk.SectionTitle : chunk.StructurePath, chunk.Index);
    }

    private static string BuildStableCitation(string filePath, string sectionTitle, int chunkIndex)
    {
        var normalizedFilePath = SanitizeCitationPart(string.IsNullOrWhiteSpace(filePath) ? "unknown" : filePath);
        var normalizedSection = SanitizeCitationPart(string.IsNullOrWhiteSpace(sectionTitle) ? "正文" : sectionTitle);
        return $"[{normalizedFilePath} | {normalizedSection} | c{chunkIndex + 1}]";
    }

    private static string SanitizeCitationPart(string text)
    {
        var normalized = text
            .Replace("\r\n", " / ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace('[', '(')
            .Replace(']', ')')
            .Replace('|', '/')
            .Trim();

        if (normalized.Length > 120)
        {
            normalized = normalized[..120].TrimEnd() + "...";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "正文" : normalized;
    }

    private string BuildChunkCacheKey(DocumentChunk chunk)
    {
        var payload = $"structured-retrieval-v2|{Path.GetFileName(EmbeddingModelPath)}|{_options.ChunkSize}|{_options.ChunkOverlap}|{chunk.FilePath}|{chunk.Index}|{chunk.StructurePath}|{chunk.ContentKind}|{BuildChunkRetrievalText(chunk)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private string GetModelPath(string fileName)
    {
        var root = _workspaceRoot ?? WorkspacePaths.FindWorkspaceRoot();
        var path = Path.Combine(root, "models", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Model file not found: {path}", path);
        }

        return path;
    }

    private void ClearLastRetrievalSnapshots()
    {
        LastRankedChunkSnapshots = [];
        LastContextChunkSnapshots = [];
        LastEvidenceDebugSnapshot = null;
        LastUsedSparsePrefilter = false;
    }

    private static RetrievalChunkSnapshot CreateRetrievalChunkSnapshot(ScoredChunk chunk)
    {
        return CreateRetrievalChunkSnapshot(chunk.Chunk);
    }

    private static RetrievalChunkSnapshot CreateRetrievalChunkSnapshot(DocumentChunk chunk)
    {
        return new RetrievalChunkSnapshot(
            BuildStableCitation(chunk),
            chunk.FilePath,
            chunk.SectionTitle,
            chunk.StructurePath,
            chunk.ContentKind,
            chunk.Text);
    }

    private static EvidenceDebugSnapshot CreateEvidenceDebugSnapshot(EvidencePlan plan, EvidenceSet set)
    {
        return new EvidenceDebugSnapshot(
            plan.Scope,
            plan.Mode,
            plan.RequiredSlots.Select(slot => slot.Name).ToArray(),
            set.CoveredSlots,
            set.IsSufficient,
            set.UnknownReason);
    }

    private sealed record IndexedChunk(DocumentChunk Chunk, float[] Embedding, IReadOnlyDictionary<string, int> TokenFrequency, int TokenCount);

    private sealed record SparseRetrievalComponent(
        IndexedChunk Indexed,
        float Bm25Raw,
        float KeywordScore,
        float TitleScore,
        float JsonStructureScore,
        float NoisePenalty,
        bool HasDirectKeywordHit,
        float Bm25Score = 0f,
        float SparseScore = 0f);

    public sealed record AddDocumentResult(string StoredPath, bool ImportedNewFile, string StatusMessage);

    public sealed record RetrievalChunkSnapshot(
        string Citation,
        string FilePath,
        string SectionTitle,
        string StructurePath,
        string ContentKind,
        string Text);

    public sealed record EvidenceDebugSnapshot(
        string Scope,
        EvidenceMode Mode,
        IReadOnlyList<string> RequiredSlots,
        IReadOnlyList<string> CoveredSlots,
        bool IsSufficient,
        string? UnknownReason);

    public enum EvidenceMode
    {
        General,
        Summary,
        Implementation,
        Composition,
        ModuleList,
        Definition,
        Compare,
        AbsenceCheck,
        Metadata,
        Usage,
        Explain
    }

    private sealed record QueryProfile(string OriginalQuestion, IReadOnlyList<string> FocusTerms, string Intent, bool WantsDetailedAnswer, string? RequestedDocumentTitle, bool AvoidsEnglishMetadata);

    private sealed record EvidenceSlot(
        string Name,
        IReadOnlyList<string> MatchTerms,
        IReadOnlyList<string> PreferredSections,
        int MinimumMatches);

    private sealed record EvidencePlan(
        string Scope,
        EvidenceMode Mode,
        IReadOnlyList<EvidenceSlot> RequiredSlots,
        IReadOnlyList<string> PreferredSections,
        IReadOnlyList<string> AllowedContentKinds,
        IReadOnlyList<string> DisallowedContentKinds,
        string SourcePreference,
        string RerankerPolicy,
        string SufficiencyRule,
        string UnknownPolicy,
        QueryProfile QueryProfile);

    private sealed record EvidenceSet(
        IReadOnlyList<DocumentChunk> Chunks,
        IReadOnlyList<string> CoveredSlots,
        bool IsSufficient,
        string? UnknownReason);

    private sealed record ScoredChunk(
        DocumentChunk Chunk,
        float Score,
        float SemanticScore,
        float Bm25Score,
        float KeywordScore,
        float TitleScore,
        float JsonStructureScore,
        float CoverageScore,
        float NeighborScore,
        float JsonBranchScore,
        bool HasDirectKeywordHit,
        float RequestedDocumentScore);

    private sealed record ProcedureCandidate(DocumentChunk Chunk, string Sentence, int Score);
    private sealed record SlotCandidate(DocumentChunk Chunk, string Sentence, int Score);
    private sealed record ExtractedEntity(string Text, string Category, DocumentChunk Chunk, int Score);

    private sealed record SummaryCandidate(DocumentChunk Chunk, string Sentence, int Score);

    private sealed record RerankRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("documents")] IReadOnlyList<string> Documents,
        [property: JsonPropertyName("top_n")] int TopN);

    private sealed record RerankScore(int Index, float Score);

    private sealed record LearnedRerankResult(IReadOnlyList<ScoredChunk> RankedChunks, bool Used, string Status);

    private sealed record RetrievalResult(
        IReadOnlyList<ScoredChunk> RankedChunks,
        IReadOnlyList<DocumentChunk> ContextChunks,
        QueryProfile QueryProfile,
        EvidencePlan EvidencePlan,
        EvidenceSet EvidenceSet,
        int SparseCandidateCount,
        int SemanticCandidateCount,
        bool UsedSparsePrefilter,
        bool UsedLearnedReranker,
        string LearnedRerankerStatus,
        IReadOnlyList<string> TargetFilePaths);

    private sealed record UsageCandidate(string Sentence, string FilePath, string SectionTitle, int ChunkIndex, int Score);

    private sealed class EmbeddingCache
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly Dictionary<string, float[]> _entries;

        private EmbeddingCache(Dictionary<string, float[]> entries)
        {
            _entries = entries;
        }

        public static async Task<EmbeddingCache> LoadAsync(string cachePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(cachePath))
            {
                return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
            }

            try
            {
                await using var stream = File.OpenRead(cachePath);
                var payload = await JsonSerializer.DeserializeAsync<EmbeddingCachePayload>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                if (payload?.Entries is null)
                {
                    return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
                }

                return new EmbeddingCache(new Dictionary<string, float[]>(payload.Entries, StringComparer.Ordinal));
            }
            catch (JsonException)
            {
                return new EmbeddingCache(new Dictionary<string, float[]>(StringComparer.Ordinal));
            }
        }

        public bool TryGet(string key, out float[] embedding)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                embedding = cached;
                return true;
            }

            embedding = Array.Empty<float>();
            return false;
        }

        public void Set(string key, float[] embedding)
        {
            _entries[key] = embedding;
        }

        public bool PruneExcept(IReadOnlySet<string> activeKeys)
        {
            var removed = false;
            foreach (var key in _entries.Keys.Except(activeKeys).ToArray())
            {
                _entries.Remove(key);
                removed = true;
            }

            return removed;
        }

        public async Task SaveAsync(string cachePath, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(cachePath);
            var payload = new EmbeddingCachePayload(_entries);
            await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record EmbeddingCachePayload(Dictionary<string, float[]> Entries);
}
