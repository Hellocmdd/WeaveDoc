using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
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

    private async Task StartRerankerIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!_options.RerankerEnabled)
        {
            return;
        }

        try
        {
            var rerankerPort = 8081;
            var baseUrl = _options.RerankerBaseUrl.TrimEnd('/');
            var portIndex = baseUrl.LastIndexOf(':');
            if (portIndex >= 0 && int.TryParse(baseUrl[(portIndex + 1)..], out var parsedPort))
            {
                rerankerPort = parsedPort;
            }

            var modelFileName = _options.RerankerModel.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                ? _options.RerankerModel
                : $"{_options.RerankerModel}.gguf";
            var modelPath = Path.Combine(WorkspaceRoot, "models", modelFileName);

            if (!File.Exists(modelPath))
            {
                return;
            }

            _rerankerProcess = new LlamaServerProcess("reranker");
            var extraArgs = $"--embedding --pooling rank --reranking --gpu-layers {_options.RerankerGpuLayerCount}";
            await _rerankerProcess.StartIfNeededAsync(modelPath, rerankerPort, extraArgs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[Reranker] Failed to start local reranker: {exception.Message}");
        }
    }

    private async Task StartChatServerIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_cloudSettings.ChatProvider == "cloud")
        {
            return;
        }

        if (!TryGetLocalServerPort(_options.LlamaServerBaseUrl, 8080, out var chatPort))
        {
            return;
        }

        var modelPath = ResolveChatModelPath();
        _chatProcess ??= new LlamaServerProcess("server");
        var gpuLayers = Environment.GetEnvironmentVariable("LLAMA_SERVER_GPU_LAYERS");
        if (string.IsNullOrWhiteSpace(gpuLayers))
        {
            gpuLayers = "auto";
        }

        var extraArgs = $"--alias {_options.ChatModel} --gpu-layers {gpuLayers.Trim()}";
        await _chatProcess.StartIfNeededAsync(modelPath, chatPort, extraArgs, cancellationToken).ConfigureAwait(false);
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

    private string ResolveChatModelPath()
    {
        var explicitModelPath = Environment.GetEnvironmentVariable("LLAMA_SERVER_MODEL");
        if (!string.IsNullOrWhiteSpace(explicitModelPath))
        {
            var fullPath = Path.GetFullPath(explicitModelPath.Trim());
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new FileNotFoundException($"Chat model file not found: {fullPath}", fullPath);
        }

        var modelsRoot = Path.Combine(WorkspaceRoot, "models");
        var preferredPath = Path.Combine(modelsRoot, "Qwen3.5-4B-Q4_K_M.gguf");
        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var excludedModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileName(EmbeddingModelPath),
            "bge-reranker-v2-m3.gguf"
        };

        var fallbackPath = Directory.EnumerateFiles(modelsRoot, "*.gguf", SearchOption.TopDirectoryOnly)
            .Where(path => !excludedModelNames.Contains(Path.GetFileName(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (fallbackPath is not null)
        {
            return fallbackPath;
        }

        throw new FileNotFoundException($"No chat GGUF model found in: {modelsRoot}", modelsRoot);
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

        _embeddingCache = await EmbeddingCache.LoadAsync(CachePath, cancellationToken).ConfigureAwait(false);
        _embeddingCacheChanged = false;
        var activeCacheKeys = new HashSet<string>(StringComparer.Ordinal);

        var corpusFiles = Directory.EnumerateFiles(docRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSupportedDocumentExtension(Path.GetExtension(path)))
            .Where(path => ShouldIndexCorpusFile(docRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in corpusFiles)
        {
            var relativePath = Path.GetRelativePath(docRoot, file).Replace('\\', '/');
            if (_excludedFiles.Contains(relativePath))
            {
                continue;
            }

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

            var embedding = _embeddingCache.TryGet(cacheKey, out var cachedEmbedding)
                ? cachedEmbedding
                : null;

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

        if (_embeddingCache.PruneExcept(activeCacheKeys))
        {
            _embeddingCacheChanged = true;
        }

        await SaveEmbeddingCacheIfChangedAsync(cancellationToken).ConfigureAwait(false);
    }

    private string BuildChunkCacheKey(DocumentChunk chunk)
    {
        var payload = $"structured-retrieval-v2|{Path.GetFileName(EmbeddingModelPath)}|{_options.ChunkSize}|{_options.ChunkOverlap}|{chunk.FilePath}|{chunk.Index}|{chunk.StructurePath}|{chunk.ContentKind}|{BuildChunkRetrievalText(chunk)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static bool TryGetLocalServerPort(string baseUrl, int fallbackPort, out int port)
    {
        port = fallbackPort;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.IsDefaultPort)
        {
            port = uri.Port;
        }

        return true;
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
}
