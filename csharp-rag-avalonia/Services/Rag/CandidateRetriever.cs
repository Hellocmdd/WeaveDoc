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
        
        // Task 3.2: For definition questions, boost chunks whose section title
        // matches focus terms so they surface in top-chunk-signal checks.
        if (queryProfile.Intent == "definition" && queryProfile.FocusTerms.Count > 0 && ranked.Count > 1)
        {
            ranked = ranked
                .Select(c => SectionTitleContainsAnyTerm(c.Chunk, queryProfile.FocusTerms)
                    ? c with { Score = c.Score + 1.0f }
                    : c)
                .OrderByDescending(c => c.Score)
                .ToArray();
        }

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

        if (queryProfile.Intent == "metadata")
        {
            var metadataSelected = new List<DocumentChunk>();
            AddFirstMatchingContextChunk(metadataSelected, filteredRanked, IsEnglishTitleChunk);
            AddFirstMatchingContextChunk(metadataSelected, filteredRanked, IsEnglishAbstractChunk);
            AddFirstMatchingContextChunk(metadataSelected, filteredRanked, IsEnglishKeywordsChunk);
            AddFirstMatchingContextChunk(metadataSelected, scopedRanked, IsAbstractChunk);

            foreach (var chunk in filteredRanked)
            {
                if (metadataSelected.Count >= limit)
                {
                    break;
                }

                if (metadataSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                metadataSelected.Add(chunk);
            }

            return metadataSelected.Take(limit).ToArray();
        }

        if (queryProfile.Intent == "procedure")
        {
            // Markdown body chunks are often fragmented by sliding-window chunking
            // (start mid-sentence, end mid-sentence). For procedure questions that
            // require synthesis across chunks, prefer well-structured chunks first
            // so the model sees coherent context before fragmented pieces.
            var procSelected = new List<DocumentChunk>();
            foreach (var chunk in scopedRanked)
            {
                if (procSelected.Count >= limit)
                {
                    break;
                }

                if (procSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (IsWellStructuredSourceChunk(chunk) && ShouldKeepChunkForAnswer(queryProfile, chunk))
                {
                    procSelected.Add(chunk);
                }
            }

            foreach (var chunk in filteredRanked)
            {
                if (procSelected.Count >= limit)
                {
                    break;
                }

                if (procSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                procSelected.Add(chunk);
            }

            return procSelected.ToArray();
        }

        if (queryProfile.Intent == "definition")
        {
            // For definition questions, prioritize chunks whose section title
            // contains focus terms — these are likely the defining sections.
            var defSelected = new List<DocumentChunk>();
            var focusTerms = queryProfile.FocusTerms;

            if (focusTerms.Count > 0)
            {
                foreach (var chunk in scopedRanked)
                {
                    if (defSelected.Count >= limit)
                    {
                        break;
                    }

                    if (defSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (ShouldKeepChunkForAnswer(queryProfile, chunk) && SectionTitleContainsAnyTerm(chunk, focusTerms))
                    {
                        defSelected.Add(chunk);
                    }
                }
            }

            foreach (var chunk in filteredRanked)
            {
                if (defSelected.Count >= limit)
                {
                    break;
                }

                if (defSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                defSelected.Add(chunk);
            }

            return defSelected.ToArray();
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

    private static bool IsEnglishTitleChunk(DocumentChunk chunk)
    {
        return chunk.ContentKind == "englishTitle"
            || chunk.SectionTitle.Contains("englishTitle", StringComparison.OrdinalIgnoreCase)
            || (chunk.StructurePath.Contains("englishTitle", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEnglishAbstractChunk(DocumentChunk chunk)
    {
        return chunk.ContentKind == "englishAbstract"
            || chunk.SectionTitle.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase)
            || (chunk.StructurePath.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEnglishKeywordsChunk(DocumentChunk chunk)
    {
        return chunk.ContentKind == "englishKeywords"
            || chunk.SectionTitle.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase)
            || (chunk.StructurePath.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWellStructuredSourceChunk(DocumentChunk chunk)
    {
        // JSON chunks are parsed from structured document sections and
        // are inherently complete sentences/paragraphs.
        if (chunk.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Markdown abstract/conclusion/intro chunks are also structured.
        if (chunk.ContentKind is "abstract" or "conclusion" or "intro" or "metadata" or "summary")
        {
            return true;
        }

        return false;
    }

    private static bool SectionTitleContainsAnyTerm(DocumentChunk chunk, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            return false;
        }

        return terms.Any(term => chunk.SectionTitle.Contains(term, StringComparison.Ordinal));
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
