using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using LLama;
using LLama.Common;
using LLama.Native;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService : IDisposable
{
    private const int EmbeddingContextSize = 8192;
    private const int EmbeddingSafetyMargin = 256;
    private const int MaxEmbeddingTokens = EmbeddingContextSize - EmbeddingSafetyMargin;
    private static readonly string[] SupportedDocumentExtensions = [".md", ".txt", ".json"];
    private static readonly string[] IgnoredCorpusRelativePrefixes = ["QA/"];

    private static readonly Regex CitationRegex = new("\\[(?:\\d+|[^\\[\\]\\r\\n|]{1,160}\\s\\|\\s[^\\[\\]\\r\\n|]{1,160}\\s\\|\\sc\\d+)\\]", RegexOptions.Compiled);
    private static readonly Regex BareChunkCitationRegex = new("\\[c(?<index>\\d+)\\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SentenceSplitRegex = new("(?<=[。！？!?])(?:\\s+)?|(?<=\\.)\\s+", RegexOptions.Compiled);
    private static readonly Regex ReferenceLineRegex = new("^(\\[?\\d+\\]?\\s*)?.{0,80}\\[J\\].*(\\d{4}|\\(\\d+\\)).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeadingLineRegex = new("^#{1,6}\\s", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarkdownImageRegex = new("!\\[[^\\]]*\\]\\([^\\)]*\\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new("\\[([^\\]]+)\\]\\([^\\)]*\\)", RegexOptions.Compiled);
    private static readonly Regex MetadataLineRegex = new("^(关键词|key words|摘要|abstract|中图分类号|文献标识码|文章编号|doi|图\\d+|表\\d+|参考文献)[:：]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingOutlineRegex = new("^\\s*(?:\\d+(?:\\.\\d+)*\\s+)+", RegexOptions.Compiled);
    private static readonly Regex JsonArrayIndexRegex = new("^\\[(\\d+)\\]$", RegexOptions.Compiled);
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
    private readonly HashSet<string> _excludedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _documentFrequency = new(StringComparer.Ordinal);

    private readonly RagOptions _options = RagOptions.LoadFromEnvironment();
    private CloudApiSettings _cloudSettings = CloudApiSettings.Load();
    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _corpusLock = new(1, 1);
    private LlamaServerProcess? _rerankerProcess;

    public CloudApiSettings CloudSettings
    {
        get => _cloudSettings;
        set => _cloudSettings = value ?? throw new ArgumentNullException(nameof(value));
    }

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

    public string LlamaServerEndpoint => _cloudSettings.ChatProvider == "cloud"
        ? _cloudSettings.CloudBaseUrl
        : _options.LlamaServerBaseUrl;

    public string LlamaServerModel => _cloudSettings.ChatProvider == "cloud"
        ? _cloudSettings.CloudModel
        : _options.ChatModel;

    public string LastRetrievalDebug { get; private set; } = "尚未执行检索。";

    public IReadOnlyList<RetrievalChunkSnapshot> LastRankedChunkSnapshots { get; private set; } = [];

    public IReadOnlyList<RetrievalChunkSnapshot> LastContextChunkSnapshots { get; private set; } = [];

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

            var chatClient = new LlamaServerChatClient(_httpClient, _options, _cloudSettings);
            await chatClient.EnsureServerAvailableAsync(cancellationToken).ConfigureAwait(false);

            await StartRerankerIfNeededAsync(cancellationToken).ConfigureAwait(false);

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
        LastUsedSparsePrefilter = retrieval.UsedSparsePrefilter;

        if (retrieval.RankedChunks.Count == 0 || retrieval.ContextChunks.Count == 0)
        {
            return "未检索到足够相关的本地文档内容。请补充更具体的问题，或确认文档来源中包含该主题。";
        }

        if (retrieval.RankedChunks[0].Score < _options.MinCombinedThreshold)
        {
            return "未检索到足够相关的本地文档内容。请补充更具体的问题，或确认文档来源中包含该主题。";
        }

        var queryProfile = BuildQueryProfile(retrievalQuestion);
        var topChunks = retrieval.ContextChunks;

        RememberActiveDocumentScope(topChunks);

        if (!queryProfile.PreferFallbackOverUnknown
            && ShouldReturnUnknownForUnsupportedRequestedDocumentTopic(queryProfile, topChunks, retrieval.TargetFilePaths))
        {
            return BuildUnknownAnswer(queryProfile, topChunks, retrieval.TargetFilePaths);
        }

        var prompt = BuildPrompt(retrievalQuestion, history, queryProfile, retrieval.RankedChunks, topChunks, retrieval.TargetFilePaths);
        var client = new LlamaServerChatClient(_httpClient, _options, _cloudSettings);
        var answer = NormalizeGeneratedAnswerCitations(
            await client.CompleteAsync(prompt, cancellationToken, RagSystemPrompt).ConfigureAwait(false),
            topChunks,
            retrieval.TargetFilePaths,
            queryProfile);

        if (IsOffTopicOrMalformedAnswer(answer, retrievalQuestion, topChunks, retrieval.TargetFilePaths))
        {
            Console.Error.WriteLine($"[QA-Flow] LLM-answer-rejected, attempting repair... q='{TruncateForLog(retrievalQuestion)}'");
            var repaired = NormalizeGeneratedAnswerCitations(
                await client.CompleteAsync(BuildRepairPrompt(retrievalQuestion, queryProfile, retrieval.RankedChunks, topChunks, retrieval.TargetFilePaths), cancellationToken, RagSystemPrompt).ConfigureAwait(false),
                topChunks,
                retrieval.TargetFilePaths,
                queryProfile);
            if (!IsOffTopicOrMalformedAnswer(repaired, retrievalQuestion, topChunks, retrieval.TargetFilePaths))
            {
                Console.Error.WriteLine("[QA-Flow] Repair-accepted");
                return repaired;
            }

            Console.Error.WriteLine("[QA-Flow] Repair-rejected, falling back to local fallback...");
            if (TryBuildLocalFallbackAnswer(queryProfile, topChunks, retrieval.TargetFilePaths, out var fallbackAnswer))
            {
                Console.Error.WriteLine("[QA-Flow] Fallback-accepted");
                return fallbackAnswer;
            }

            Console.Error.WriteLine("[QA-Flow] Fallback-empty, returning unknown...");
            if (!queryProfile.PreferFallbackOverUnknown
                && ShouldReturnUnknownForUnsupportedRequestedDocumentTopic(queryProfile, topChunks, retrieval.TargetFilePaths))
            {
                return BuildUnknownAnswer(queryProfile, topChunks, retrieval.TargetFilePaths);
            }

            return BuildUnknownAnswer(queryProfile, topChunks, retrieval.TargetFilePaths);
        }

        Console.Error.WriteLine("[QA-Flow] LLM-answer-accepted");
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
            var relativeExistingPath = Path.GetRelativePath(DocumentRoot, fullSourcePath).Replace('\\', '/');
            _excludedFiles.Remove(relativeExistingPath);
            await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
            return new AddDocumentResult(relativeExistingPath, false, "文档已在知识库目录中，已直接刷新索引。");
        }

        var existingDuplicate = Directory.EnumerateFiles(DocumentRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSupportedDocumentExtension(Path.GetExtension(path)))
            .FirstOrDefault(path => FilesHaveSameContent(fullSourcePath, path));

        if (!string.IsNullOrWhiteSpace(existingDuplicate))
        {
            var relativeExistingPath = Path.GetRelativePath(DocumentRoot, existingDuplicate).Replace('\\', '/');
            _excludedFiles.Remove(relativeExistingPath);
            await ReloadCorpusAsync(cancellationToken).ConfigureAwait(false);
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

        _excludedFiles.Add(normalized);
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

    public void Dispose()
    {
        _rerankerProcess?.Dispose();
        _embedder?.Dispose();
        _embeddingWeights?.Dispose();
        _httpClient.Dispose();
        _corpusLock.Dispose();
        _initializationLock.Dispose();
    }
}
