using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed partial class LocalAiService
{
    private async Task<RetrievalResult> FindRelevantChunksAsync(string question, int count, CancellationToken cancellationToken)
    {
        if (_embedder is null)
        {
            throw new InvalidOperationException("Embedding model is not initialized.");
        }

        var queryProfile = BuildQueryProfile(question);
        
        // Task 2.2: Hard filtering based on FilePath
        var targetFilePaths = ResolveRetrievalScopeFilePaths(question, queryProfile);
        var targetFilePathSet = targetFilePaths.Count > 0 
            ? new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase) 
            : null;

        var scopedIndexedChunks = targetFilePathSet is null
            ? _indexedChunks.ToArray()
            : _indexedChunks.Where(item => targetFilePathSet.Contains(item.Chunk.FilePath)).ToArray();

        if (scopedIndexedChunks.Length == 0)
        {
            return new RetrievalResult(
                [],
                [],
                queryProfile,
                0,
                0,
                false,
                false,
                "no scoped chunks",
                targetFilePaths,
                _options.PipelineMode);
        }

        // Task 2.1: Step 1 - Only rely on vector cosine similarity
        var questionVector = await GetEmbeddingVectorAsync(question, cancellationToken).ConfigureAwait(false);
        var vectorCandidateLimit = Math.Clamp(_options.SparseCandidatePoolSize, 50, 100);
        
        var candidates = scopedIndexedChunks
            .Select(indexed =>
            {
                var semanticScore = CosineSimilarity(questionVector, indexed.Embedding);
                return new ScoredChunk(indexed.Chunk, semanticScore, semanticScore, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false, 0f);
            })
            .OrderByDescending(item => item.Score)
            .Take(vectorCandidateLimit) // Task 2.1: Step 2 - Extract Top-100
            .ToArray(); // Task 2.1: Step 3 - Completely remove BM25

        // Task 3.1: Send Top-100 to Reranker without intervention
        var ruleRerankCount = candidates.Length;
        // Apply simplified metadata policy is deleted (Task 1.2)
        var globalLearnedRerank = await TryRerankWithLearnedModelAsync(question, candidates, queryProfile, ruleRerankCount, cancellationToken).ConfigureAwait(false);
        var ranked = globalLearnedRerank.RankedChunks;
        
        // Task 3.2: Strictly ordered by Reranker score, Top-8 to Top-12
        var limit = queryProfile.WantsDetailedAnswer ? 12 : 8;
        var contextChunks = BuildAnswerContextChunks(ranked, queryProfile, targetFilePaths, limit);

        return new RetrievalResult(
            ranked,
            contextChunks,
            queryProfile,
            0,
            candidates.Length,
            false,
            globalLearnedRerank.Used,
            $"global:{globalLearnedRerank.Status}",
            targetFilePaths,
            _options.PipelineMode);
    }

    private IReadOnlyList<string> ResolveRetrievalScopeFilePaths(string question, QueryProfile queryProfile)
    {
        var explicitFilePaths = ApplyRequestedFileFormatPreference(question, queryProfile.Intent, ResolveRequestedDocumentFilePaths(queryProfile.RequestedDocumentTitle));
        if (explicitFilePaths.Count > 0 || !string.IsNullOrWhiteSpace(queryProfile.RequestedDocumentTitle))
        {
            return explicitFilePaths;
        }

        if (ShouldUseActiveDocumentScope(queryProfile) && _activeDocumentFilePaths.Count > 0)
        {
            return ApplyRequestedFileFormatPreference(question, queryProfile.Intent, _activeDocumentFilePaths);
        }

        return [];
    }

    private static IReadOnlyList<DocumentChunk> BuildAnswerContextChunks(
        IReadOnlyList<ScoredChunk> ranked,
        QueryProfile queryProfile,
        IReadOnlyList<string> targetFilePaths,
        int limit)
    {
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);

        var scopedRanked = ranked
            .Select(item => item.Chunk)
            .Where(chunk => targetFilePathSet is null || targetFilePathSet.Contains(chunk.FilePath))
            .ToList();

        var filteredRanked = scopedRanked
            .Where(chunk => ShouldKeepChunkForAnswer(queryProfile, chunk))
            .ToList();

        if (filteredRanked.Count == 0)
        {
            filteredRanked = scopedRanked;
        }

        if (queryProfile.Intent != "summary")
        {
            return filteredRanked.Take(limit).ToArray();
        }

        var selected = new List<DocumentChunk>();
        AddFirstMatchingContextChunk(selected, filteredRanked, IsAbstractChunk);
        AddFirstMatchingContextChunk(selected, scopedRanked, IsOverviewChunk);

        foreach (var chunk in filteredRanked)
        {
            if (selected.Count >= limit)
            {
                break;
            }

            if (selected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            selected.Add(chunk);
        }

        return selected.Take(limit).ToArray();
    }

    private static void AddFirstMatchingContextChunk(
        List<DocumentChunk> selected,
        IReadOnlyList<DocumentChunk> chunks,
        Func<DocumentChunk, bool> predicate)
    {
        var chunk = chunks.FirstOrDefault(predicate);
        if (chunk is null)
        {
            return;
        }

        if (selected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        selected.Add(chunk);
    }

    private async Task<float[]> GetEmbeddingVectorAsync(string text, CancellationToken cancellationToken)
    {
        if (_embedder is null)
        {
            throw new InvalidOperationException("Embedding model is not initialized.");
        }

        var embeddingText = PrepareTextForEmbedding(text);
        var embeddings = await _embedder.GetEmbeddings(embeddingText, cancellationToken).ConfigureAwait(false);
        var result = embeddings.FirstOrDefault();
        if (result is null || result.Length == 0)
        {
            throw new InvalidOperationException($"Embedder returned no embedding for text (length={text.Length}).");
        }
        return result;
    }
}
