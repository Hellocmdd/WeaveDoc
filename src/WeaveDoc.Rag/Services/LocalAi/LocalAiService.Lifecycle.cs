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
