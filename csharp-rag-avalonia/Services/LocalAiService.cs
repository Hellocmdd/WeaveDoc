using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using LLama;
using LLama.Common;
using LLama.Native;
using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed class LocalAiService : IDisposable
{
    private const int EmbeddingContextSize = 512;
    private const int EmbeddingSafetyMargin = 64;
    private const int MaxEmbeddingTokens = EmbeddingContextSize - EmbeddingSafetyMargin;
    private static readonly string[] SupportedDocumentExtensions = [".md", ".txt", ".json"];

    private static readonly Regex QueryTokenRegex = new("[a-z0-9]+|[\\u4e00-\\u9fff]{2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CitationRegex = new("\\[(?:\\d+|[^\\[\\]\\r\\n|]{1,160}\\s\\|\\s[^\\[\\]\\r\\n|]{1,160}\\s\\|\\sc\\d+)\\]", RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new("(?<=[。！？!?])\\s+|(?<=\\.)\\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingGreetingRegex = new("^(?:\\s*(?:你好|您好|嗨|哈喽|hello|hi|hey|请问|麻烦问一下|我想问一下|想请教一下|请教一下)\\s*[,，.。!！?？:：;；/+|、-]*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BoundaryNoiseRegex = new("^[\\s,，.。!！?？:：;；/+|、-]+|[\\s,，.。!！?？:：;；/+|、-]+$", RegexOptions.Compiled);
    private static readonly Regex ReferenceLineRegex = new("^(\\[?\\d+\\]?\\s*)?.{0,80}\\[J\\].*(\\d{4}|\\(\\d+\\)).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HeadingLineRegex = new("^#{1,6}\\s", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MetadataLineRegex = new("^(关键词|key words|摘要|abstract|中图分类号|文献标识码|文章编号|doi|图\\d+|表\\d+|参考文献)[:：]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> FocusStopWords =
    [
        "什么", "哪些", "哪个", "那个", "这个", "一下", "一些", "其中", "对于", "相关", "关于", "有关", "内容",
        "文档", "资料", "介绍", "说明", "总结", "概述", "一下子", "请问", "你好", "您好", "可以", "是否",
        "怎么", "如何", "为什么", "原因", "原理", "机制", "作用", "用途", "功能", "区别", "比较", "对比",
        "流程", "步骤", "实现", "定义", "概念", "方法", "情况", "一下吗", "一下呢"
    ];
    private static readonly string[] ExplanationSignals = ["原因", "因为", "导致", "所以", "由于", "为了", "通过", "原理", "机制"];
    private static readonly string[] ProcedureSignals = ["步骤", "流程", "首先", "然后", "最后", "实现", "方法", "过程"];
    private static readonly string[] UsageSignals = ["用于", "作用", "用途", "负责", "实现", "控制", "采集", "识别", "管理"];
    private static readonly string[] DetailSignals = ["详细", "具体", "展开", "细说", "详述", "全面", "细一点", "详细一点", "具体一点", "展开讲", "展开说", "多说", "讲讲", "说说"];
    private static readonly HashSet<string> JsonIgnoredFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "englishtitle", "englishaffiliation", "englishabstract", "englishkeywords",
        "classificationnumber", "documentidentifier", "articlenumber", "doi"
    };
    private static readonly HashSet<string> JsonPriorityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "summary", "content", "overview", "architecture", "workflow", "keywords"
    };
    private static readonly HashSet<string> JsonMetadataFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authors", "author", "affiliation", "englishaffiliation", "classificationnumber",
        "documentidentifier", "articlenumber", "doi", "englishTitle", "englishAbstract", "englishKeywords"
    };

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

    public string EmbeddingModelPath => GetModelPath("sentence-transformers--all-MiniLM-L6-v2.gguf");

    public string WorkspaceRoot => _workspaceRoot ?? WorkspacePaths.FindWorkspaceRoot();

    public string DocumentRoot => Path.Combine(WorkspaceRoot, "doc");

    public string CachePath => Path.Combine(WorkspaceRoot, ".rag", "embedding-cache.json");

    public int CorpusChunkCount => _chunks.Count;

    public IReadOnlyList<string> CorpusFiles => _corpusFiles;

    public string LlamaServerEndpoint => _options.LlamaServerBaseUrl;

    public string LlamaServerModel => _options.ChatModel;

    public string LastRetrievalDebug { get; private set; } = "尚未执行检索。";

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
        var prompt = BuildPrompt(retrievalQuestion, history, queryProfile, retrieval.RankedChunks, topChunks);
        var client = new LlamaServerChatClient(_httpClient, _options);
        var answer = await client.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        if (IsOffTopicOrMalformedAnswer(answer, retrievalQuestion))
        {
            var repaired = await client.CompleteAsync(BuildRepairPrompt(retrievalQuestion, queryProfile, retrieval.RankedChunks, topChunks), cancellationToken).ConfigureAwait(false);
            if (!IsOffTopicOrMalformedAnswer(repaired, retrievalQuestion))
            {
                return repaired;
            }

            return BuildExtractiveFallbackAnswer(retrievalQuestion, queryProfile, topChunks);
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
            PoolingType = LLamaPoolingType.Mean,
            UseMemorymap = true,
            GpuLayerCount = 0,
            FlashAttention = true,
        };

        _embeddingWeights?.Dispose();
        _embeddingWeights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken).ConfigureAwait(false);

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

        var queryTokens = ExtractQueryTokens(question);
        var queryProfile = BuildQueryProfile(question);

        var sparseComponents = _indexedChunks
            .Select(indexed =>
            {
                var bm25Raw = ComputeBm25(queryTokens, indexed);
                var (keywordScore, hasDirectKeywordHit) = ComputeKeywordScore(queryTokens, BuildChunkRetrievalText(indexed.Chunk));
                var titleScore = ComputeTitleScore(queryProfile.FocusTerms, indexed.Chunk);
                var noisePenalty = ComputeNoisePenalty(indexed.Chunk);

                return new SparseRetrievalComponent(indexed, bm25Raw, keywordScore, titleScore, noisePenalty, hasDirectKeywordHit);
            })
            .ToArray();

        var maxBm25 = sparseComponents.Length == 0 ? 1f : Math.Max(1e-6f, sparseComponents.Max(item => item.Bm25Raw));
        var sparseRanked = sparseComponents
            .Select(item =>
            {
                var bm25Normalized = item.Bm25Raw / maxBm25;
                var sparseScore = (bm25Normalized * _options.Bm25Weight)
                    + (item.KeywordScore * _options.KeywordWeight)
                    + (item.TitleScore * _options.TitleWeight)
                    + (item.HasDirectKeywordHit ? _options.DirectKeywordBonus : 0f)
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

                return new ScoredChunk(item.Indexed.Chunk, score, semanticScore, item.Bm25Score, item.KeywordScore, item.TitleScore, 0f, 0f, item.HasDirectKeywordHit);
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Chunk.Index)
            .ToArray();

        var candidates = candidatePool
            .Take(Math.Min(_options.CandidatePoolSize, candidatePool.Length))
            .ToArray();

        var reranked = RerankCandidates(candidates, queryProfile, count);
        var contextChunks = BuildContextWindow(reranked);
        return new RetrievalResult(
            reranked,
            contextChunks,
            queryProfile,
            sparseRanked.Length,
            semanticInputs.Length,
            hasMeaningfulSparseEvidence);
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

    private static bool IsSupportedDocumentExtension(string extension)
    {
        return SupportedDocumentExtensions.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));
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
        var raw = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        if (!Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
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

        foreach (var name in new[] { "title", "name", "documentTitle", "document_name" })
        {
            if (TryGetPropertyIgnoreCase(root, name, out var value))
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

            if (JsonIgnoredFieldNames.Contains(property.Name))
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

            if (JsonPriorityFieldNames.Contains(property.Name) || value.Length > _options.ChunkSize / 2)
            {
                FlushJsonScalarChunk(chunks, scalarLines, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex);
                AddStructuredChunk(
                    chunks,
                    sourceName,
                    relativePath,
                    documentTitle,
                    childSection,
                    childPath,
                    GetJsonContentKind(property.Name),
                    ref chunkIndex,
                    $"{property.Name}: {value}");
                continue;
            }

            scalarLines.Add($"{property.Name}: {value}");
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
            var itemPath = $"{structurePath}[{itemIndex}]";
            var itemSection = GetLeafStructureSegment(itemPath);

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
            GetJsonContentKind(GetLeafStructureSegment(structurePath)),
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
            GetJsonContentKind(GetLeafStructureSegment(structurePath)),
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

        if (normalized.Equals("title", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("name", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("documentTitle", StringComparison.OrdinalIgnoreCase))
        {
            return "title";
        }

        if (normalized.Equals("abstract", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("summary", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("overview", StringComparison.OrdinalIgnoreCase))
        {
            return "summary";
        }

        if (normalized.Equals("keywords", StringComparison.OrdinalIgnoreCase))
        {
            return "keyword";
        }

        if (normalized.Equals("content", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        if (JsonMetadataFieldNames.Contains(normalized))
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
        var parts = new List<string>(4);
        AppendRetrievalPart(parts, chunk.DocumentTitle);
        AppendRetrievalPart(parts, chunk.StructurePath);
        AppendRetrievalPart(parts, chunk.SectionTitle);
        AppendRetrievalPart(parts, chunk.ContentKind is "body" or "" ? string.Empty : chunk.ContentKind);
        AppendRetrievalPart(parts, chunk.Text);
        return string.Join('\n', parts);
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

    private Dictionary<string, int> BuildTokenFrequency(string text)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in ExtractQueryTokens(text))
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

        builder.AppendLine($"稀疏预筛候选: {retrieval.SparseCandidateCount} 条");
        builder.AppendLine($"进入语义计算: {retrieval.SemanticCandidateCount} 条");
        builder.AppendLine($"是否使用稀疏预筛: {(retrieval.UsedSparsePrefilter ? "是" : "否（回退全量语义）")}");

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

            builder.AppendLine($"[{index + 1}] score={item.Score:F3} (semantic={item.SemanticScore:F3}, bm25={item.Bm25Score:F3}, keyword={item.KeywordScore:F3}, title={item.TitleScore:F3}, coverage={item.CoverageScore:F3}, neighbor={item.NeighborScore:F3}, directHit={item.HasDirectKeywordHit}) | {BuildStableCitation(item.Chunk)}");
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
        var focusTerms = ExtractQueryTokens(question)
            .Where(IsMeaningfulFocusTerm)
            .OrderByDescending(token => token.Length)
            .Take(8)
            .ToArray();

        var intent = DetectIntent(question);
        var wantsDetailedAnswer = WantsDetailedAnswer(question);
        return new QueryProfile(question, focusTerms, intent, wantsDetailedAnswer);
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

        var hasAsciiLetterOrDigit = token.Any(character => char.IsAsciiLetterOrDigit(character));
        if (!hasAsciiLetterOrDigit)
        {
            if (FocusStopWords.Any(stopWord => token.Contains(stopWord, StringComparison.Ordinal)))
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

        if (question.Contains("如何", StringComparison.Ordinal) || question.Contains("怎么", StringComparison.Ordinal) || question.Contains("步骤", StringComparison.Ordinal) || question.Contains("流程", StringComparison.Ordinal))
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
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var chunks = new List<DocumentChunk>();

        var documentTitle = ExtractDocumentTitle(normalized, sourceName);
        var sectionTitle = documentTitle;
        var chunkIndex = 0;
        var buffer = new StringBuilder();

        foreach (var paragraph in SplitParagraphs(normalized))
        {
            if (IsHeadingParagraph(paragraph))
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, ref chunkIndex);
                sectionTitle = paragraph.TrimStart('#', ' ').Trim();
                buffer.AppendLine(paragraph.Trim());
                continue;
            }

            if (buffer.Length > 0 && buffer.Length + paragraph.Length + 2 > _options.ChunkSize)
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, ref chunkIndex);
            }

            if (paragraph.Length > _options.ChunkSize)
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, ref chunkIndex);
                foreach (var oversizedPart in SplitLargeParagraph(paragraph))
                {
                    chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, oversizedPart, Array.Empty<float>(), documentTitle, sectionTitle, string.Empty, "body"));
                }

                continue;
            }

            if (buffer.Length > 0)
            {
                buffer.AppendLine();
            }

            buffer.Append(paragraph.Trim());
        }

        FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, ref chunkIndex);

        return chunks;
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
            || (trimmed.Length <= 40 && !trimmed.Contains('。') && !trimmed.Contains('，') && !trimmed.Contains('.'));
    }

    private void FlushChunkBuffer(List<DocumentChunk> chunks, StringBuilder buffer, string sourceName, string relativePath, string documentTitle, string sectionTitle, ref int chunkIndex)
    {
        var text = buffer.ToString().Trim();
        buffer.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, text, Array.Empty<float>(), documentTitle, sectionTitle, string.Empty, "body"));
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
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in QueryTokenRegex.Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            if (token.Length < 2)
            {
                continue;
            }

            tokens.Add(token);
            if (ContainsCjk(token) && token.Length > 3)
            {
                for (var index = 0; index < token.Length - 1; index++)
                {
                    tokens.Add(token.Substring(index, 2));
                }
            }
        }

        return tokens.ToArray();
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

    private ScoredChunk[] RerankCandidates(IReadOnlyList<ScoredChunk> candidates, QueryProfile queryProfile, int count)
    {
        var ranked = candidates
            .Select(candidate =>
            {
                var coverageScore = ComputeCoverageScore(queryProfile.FocusTerms, candidate.Chunk);
                var neighborScore = ComputeNeighborSupportScore(queryProfile.FocusTerms, candidate.Chunk);
                var intentBoost = ComputeIntentBoost(queryProfile.Intent, candidate.Chunk.Text);
                var finalScore = candidate.Score
                    + (coverageScore * _options.CoverageWeight)
                    + (neighborScore * _options.NeighborWeight)
                    + intentBoost;

                return candidate with
                {
                    Score = finalScore,
                    CoverageScore = coverageScore,
                    NeighborScore = neighborScore
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

    private static float ComputeIntentBoost(string intent, string chunkText)
    {
        var signals = intent switch
        {
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

    private IReadOnlyList<DocumentChunk> BuildContextWindow(IReadOnlyList<ScoredChunk> rankedChunks)
    {
        if (rankedChunks.Count == 0)
        {
            return [];
        }

        var selected = new Dictionary<string, DocumentChunk>(StringComparer.Ordinal);
        foreach (var ranked in rankedChunks)
        {
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
        }

        return selected.Values
            .OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Index)
            .ToArray();
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

    private string BuildPrompt(string question, IReadOnlyList<ChatTurn> history, QueryProfile queryProfile, IReadOnlyList<ScoredChunk> rankedChunks, IReadOnlyList<DocumentChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是一个本地文档问答助手。你必须严格基于下方“上下文”回答，不得使用外部常识扩写。");
        builder.AppendLine("回答规则：");
        builder.AppendLine("1) 若上下文不足或不包含答案，直接回答“我不知道（当前文档未覆盖）”。");
        builder.AppendLine("2) 给出结论时，必须在句末标注稳定来源标签，直接复制上下文中的“来源标签”，例如 [doc/a.md | 方法设计 | c3]。禁止只写 [1]、[2] 这种临时编号。");
        builder.AppendLine($"3) {BuildAnswerStructureRule(queryProfile)}");
        builder.AppendLine("4) 禁止直接输出论文题目、作者名单、单位信息、HTML/Markdown 标题块。");
        builder.AppendLine("5) 如果多个来源能互相补充，要综合整理后回答，而不是逐条照抄。");
        builder.AppendLine("6) 回答语言必须与用户问题一致：中文问题必须用中文回答。");
        builder.AppendLine($"7) 当前问题类型: {DescribeIntent(queryProfile.Intent)}。请按这个类型组织答案。");
        builder.AppendLine($"8) {BuildDomainGuidance(queryProfile)}");
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

    private string BuildRepairPrompt(string question, QueryProfile queryProfile, IReadOnlyList<ScoredChunk> rankedChunks, IReadOnlyList<DocumentChunk> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("你是本地文档问答助手。上一次回答偏离问题。现在必须重新回答。\n");
        builder.AppendLine("硬性要求：");
        builder.AppendLine($"1) 仅依据下面上下文回答，不要输出与问题或者给定材料无关的题目模板、算法示例、代码题。\n2) 禁止输出论文题目、作者名单、单位信息、HTML/Markdown 标题块。\n3) 每句必须附稳定来源标签，直接复制上下文中的“来源标签”，例如 [doc/a.md | 方法设计 | c3]。\n4) 回答语言必须与用户问题一致（中文问中文答）。\n5) {BuildAnswerStructureRule(queryProfile)}\n6) {BuildDomainGuidance(queryProfile)}\n7) 若上下文不足，仅输出：我不知道（当前文档未覆盖）。");
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
            "compare" => "对比题，先说主要差异，再分点比较",
            "explain" => "解释题，先说结论，再说明原因/机制",
            "procedure" => "流程题，按步骤说明",
            "usage" => "用途题，先说用途，再列关键点",
            "summary" => "总结题，先概括主题，再分层展开内容",
            "definition" => "定义题，先给定义，再补充特征或作用",
            _ => "一般问答，先结论后依据"
        };
    }

    private bool IsOffTopicOrMalformedAnswer(string answer, string question)
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

        if (IsReferenceStyleAnswer(normalized))
        {
            return true;
        }

        var tokens = ExtractQueryTokens(question);
        if (tokens.Count == 0)
        {
            return false;
        }

        var lowerAnswer = normalized.ToLowerInvariant();
        var overlap = tokens.Count(token => lowerAnswer.Contains(token, StringComparison.Ordinal));
        return overlap == 0;
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

    private string BuildExtractiveFallbackAnswer(string question, QueryProfile queryProfile, IReadOnlyList<DocumentChunk> chunks)
    {
        // 对“作用/用途/功能”类问题走专门的结构化回退，避免残句和题头片段
        if (IsUsageQuestion(question))
        {
            var usage = BuildUsageFallbackAnswer(question, chunks);
            if (!string.IsNullOrWhiteSpace(usage))
            {
                return usage;
            }
        }

        if (queryProfile.Intent == "procedure")
        {
            var procedure = BuildProcedureFallbackAnswer(question, chunks);
            if (!string.IsNullOrWhiteSpace(procedure))
            {
                return procedure;
            }
        }

        var tokens = ExtractQueryTokens(question);
        var candidates = new List<(DocumentChunk Chunk, string Sentence, int Score)>();

        for (var sourceIndex = 0; sourceIndex < chunks.Count; sourceIndex++)
        {
            var chunk = chunks[sourceIndex];
            var sentences = SentenceSplitRegex.Split(chunk.Text)
                .Select(sentence => sentence.Trim())
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .Take(6);

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
                builder.AppendLine();
            }

            builder.Append($"{item.Sentence} {BuildStableCitation(item.Chunk)}");
        }

        return builder.ToString();
    }

    private string? BuildProcedureFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var queryTokens = ExtractQueryTokens(question);
        var candidates = new List<ProcedureCandidate>();

        foreach (var chunk in chunks)
        {
            var structureBoost = CountProcedureStructureSignals($"{chunk.StructurePath} {chunk.SectionTitle}");
            var chunkSignals = CountArchitectureSignals($"{chunk.StructurePath} {chunk.SectionTitle} {chunk.Text}");
            var sentences = SentenceSplitRegex.Split(chunk.Text)
                .Select(sentence => sentence.Trim())
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .Take(10);

            foreach (var sentence in sentences)
            {
                if (!IsCandidateSentence(sentence))
                {
                    continue;
                }

                var lowerSentence = sentence.ToLowerInvariant();
                var tokenHits = queryTokens.Count(token => lowerSentence.Contains(token, StringComparison.Ordinal));
                var procedureHits = ProcedureSignals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
                var architectureHits = CountArchitectureSignals(sentence);
                var methodHits = CountMethodSignals(sentence);

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
        builder.AppendLine("根据当前文档，系统实现精准灌溉主要依靠以下环节：");
        for (var index = 0; index < selected.Length; index++)
        {
            builder.AppendLine($"{index + 1}. {selected[index].Sentence} {BuildStableCitation(selected[index].Chunk)}");
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
            || question.Contains("主要内容", StringComparison.Ordinal)
            || question.Contains("主要讲", StringComparison.Ordinal)
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
            || question.Contains("再说", StringComparison.Ordinal)
            || question.Contains("继续", StringComparison.Ordinal)
            || question.Contains("展开一点", StringComparison.Ordinal)
            || question.Contains("具体呢", StringComparison.Ordinal)
            || question.Contains("细一点", StringComparison.Ordinal);
    }

    private static string BuildAnswerStructureRule(QueryProfile queryProfile)
    {
        if (queryProfile.WantsDetailedAnswer)
        {
            return "先给直接结论，再充分展开；优先用 1 段概括 + 4-6 个要点，或 2-4 个自然段说明，每个要点尽量写成完整句，不要只给几条很短的并列短语。";
        }

        return "先给直接结论，再用 2-4 点展开依据；不要只复述一句原文。";
    }

    private static string BuildDomainGuidance(QueryProfile queryProfile)
    {
        if (queryProfile.Intent == "summary")
        {
            if (queryProfile.WantsDetailedAnswer)
            {
                return "如果问题是在问论文或文档主要写什么，优先概括研究目标，再展开写系统组成、关键方法、实现/实验结果、结论或意义；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，不要泛化改写。";
            }

            return "如果问题是在问论文或文档主要写什么，先概括主旨，再列出几个核心内容点；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，不要泛化改写。";
        }

        return queryProfile.WantsDetailedAnswer
            ? "回答时补足背景、关键细节和上下游关系，但不要脱离上下文扩写。"
            : "回答应紧扣问题，不要无关延伸。";
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

    private static string? ExtractQuestionSubject(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var matches = QueryTokenRegex.Matches(question.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length >= 2)
            .Where(token => token is not "什么" and not "作用" and not "用途" and not "功能" and not "有什么" and not "一下" and not "请问")
            .ToArray();

        if (matches.Length == 0)
        {
            return null;
        }

        return matches.OrderByDescending(token => token.Length).First();
    }

    private string? BuildUsageFallbackAnswer(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var subject = ExtractQuestionSubject(question);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var topSentences = CollectUsageCandidates(subject, chunks);
        if (topSentences.Count == 0)
        {
            topSentences = CollectUsageCandidates(subject, _chunks);
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

            var parts = SentenceSplitRegex.Split(chunk.Text)
                .Select(sentence => sentence.Trim())
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .Take(12);

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

                if (chunk.SectionTitle.Contains("主控", StringComparison.Ordinal) || chunk.SectionTitle.Contains("控制", StringComparison.Ordinal))
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
        var signals = new[] { "核心控制器", "主控芯片", "主控制器", "控制核心", "数据采集", "决策控制", "执行机构", "交互显示", "多任务调度", "感知层", "决策层", "执行层", "交互层", "模糊pid", "电磁阀", "土壤湿度" };
        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
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
            .Select(line => line.Trim())
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

    private sealed record IndexedChunk(DocumentChunk Chunk, float[] Embedding, IReadOnlyDictionary<string, int> TokenFrequency, int TokenCount);

    private sealed record SparseRetrievalComponent(
        IndexedChunk Indexed,
        float Bm25Raw,
        float KeywordScore,
        float TitleScore,
        float NoisePenalty,
        bool HasDirectKeywordHit,
        float Bm25Score = 0f,
        float SparseScore = 0f);

    public sealed record AddDocumentResult(string StoredPath, bool ImportedNewFile, string StatusMessage);

    private sealed record QueryProfile(string OriginalQuestion, IReadOnlyList<string> FocusTerms, string Intent, bool WantsDetailedAnswer);

    private sealed record ScoredChunk(
        DocumentChunk Chunk,
        float Score,
        float SemanticScore,
        float Bm25Score,
        float KeywordScore,
        float TitleScore,
        float CoverageScore,
        float NeighborScore,
        bool HasDirectKeywordHit);

    private sealed record ProcedureCandidate(DocumentChunk Chunk, string Sentence, int Score);

    private sealed record RetrievalResult(
        IReadOnlyList<ScoredChunk> RankedChunks,
        IReadOnlyList<DocumentChunk> ContextChunks,
        QueryProfile QueryProfile,
        int SparseCandidateCount,
        int SemanticCandidateCount,
        bool UsedSparsePrefilter);

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
