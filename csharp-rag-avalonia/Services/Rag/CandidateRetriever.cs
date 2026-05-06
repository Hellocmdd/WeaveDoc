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
        
        // Intent-level nudges keep structurally relevant chunks visible after
        // reranking without baking in document- or case-specific terms.
        if (queryProfile.Intent is "definition" or "compare" or "procedure" or "summary" && ranked.Count > 1)
        {
            ranked = ranked
                .Select(c => c with { Score = c.Score + ComputeTopChunkIntentBoost(c.Chunk, queryProfile) })
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
            AddFirstMatchingContextChunk(metadataSelected, filteredRanked, IsAbstractChunk);

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

                // Keep at most one chunk per English content kind to avoid
                // englishAbstract segments (c11,c12,c13) crowding the window.
                if (IsEnglishFieldChunk(chunk)
                    && metadataSelected.Any(item => string.Equals(item.ContentKind, chunk.ContentKind, StringComparison.OrdinalIgnoreCase)))
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

        if (queryProfile.Intent == "compare")
        {
            var compareSelected = new List<DocumentChunk>();
            var compareTerms = BuildIntentTerms(queryProfile);

            if (queryProfile.CompareSubjects.Count >= 2)
            {
                foreach (var chunk in filteredRanked)
                {
                    if (compareSelected.Count >= limit)
                    {
                        break;
                    }

                    if (compareSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (ChunkMatchesCompareSubjects(chunk, queryProfile.CompareSubjects))
                    {
                        compareSelected.Add(chunk);
                    }
                }
            }

            foreach (var chunk in filteredRanked)
            {
                if (compareSelected.Count >= limit)
                {
                    break;
                }

                if (compareSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (compareTerms.Count == 0 || ChunkMatchesAnyTerm(chunk, compareTerms))
                {
                    compareSelected.Add(chunk);
                }
            }

            foreach (var chunk in filteredRanked)
            {
                if (compareSelected.Count >= limit)
                {
                    break;
                }

                if (!compareSelected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    compareSelected.Add(chunk);
                }
            }

            return compareSelected.ToArray();
        }

        if (queryProfile.Intent != "summary")
        {
            return filteredRanked.Take(limit).ToArray();
        }

        var selected = new List<DocumentChunk>();
        var summaryCoreChunks = filteredRanked
            .Where(chunk => IsSummaryLeadCandidate(chunk, queryProfile))
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = GetSummaryContextPriority(chunk, queryProfile)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .Take(2)
            .ToArray();

        foreach (var chunk in summaryCoreChunks)
        {
            AddSpecificChunk(selected, chunk);
        }

        var summarySupportChunks = filteredRanked
            .Where(chunk => !selected.Any(item => item.Index == chunk.Index && string.Equals(item.FilePath, chunk.FilePath, StringComparison.OrdinalIgnoreCase)))
            .Where(chunk => IsSummarySupportCandidate(chunk, queryProfile))
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = GetSummarySupportPriority(chunk)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .ToArray();

        foreach (var chunk in summarySupportChunks.Concat(filteredRanked))
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

    private static void AddSpecificChunk(List<DocumentChunk> selected, DocumentChunk? chunk)
    {
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

    internal static float ComputeTopChunkIntentBoost(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var score = 0f;
        var retrievalText = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        var sectionTitle = chunk.SectionTitle.ToLowerInvariant();
        var structurePath = chunk.StructurePath.ToLowerInvariant();

        foreach (var term in BuildIntentTerms(queryProfile))
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (sectionTitle.Contains(term, StringComparison.Ordinal))
            {
                score += 0.9f;
            }

            if (structurePath.Contains(term, StringComparison.Ordinal))
            {
                score += 0.7f;
            }

            if (retrievalText.Contains(term, StringComparison.Ordinal))
            {
                score += 0.35f;
            }
        }

        if (chunk.ContentKind == "body")
        {
            score += 0.35f;
        }

        if (IsWellStructuredSourceChunk(chunk))
        {
            score += 0.2f;
        }

        if (queryProfile.Intent == "compare")
        {
            score += CountSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}", CompareSignals) * 0.15f;
            if (queryProfile.CompareSubjects.Count >= 2 && ChunkMatchesCompareSubjects(chunk, queryProfile.CompareSubjects))
            {
                score += 0.8f;
            }
        }

        if (queryProfile.Intent == "procedure")
        {
            score += CountProcedureStructureSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}") * 0.12f;
            score += ComputeProcedureTopChunkQualityBoost(chunk);
        }

        if (queryProfile.Intent == "summary" && IsSummaryLeadCandidate(chunk, queryProfile))
        {
            score += 0.45f;
        }

        return score;
    }

    private static float ComputeProcedureTopChunkQualityBoost(DocumentChunk chunk)
    {
        var score = 0f;
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";

        if (chunk.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            score += 1.4f;
        }

        if (chunk.ContentKind == "body")
        {
            score += 0.8f;
        }

        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            score += 0.35f;
        }

        if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            score += 0.75f;
        }

        score += Math.Min(10, CountSignals(source,
            "输入", "前提", "处理", "执行", "输出", "结果", "控制", "调节", "机制", "策略",
            "算法", "反馈", "误差", "驱动", "补偿", "判断", "计算", "生成", "监测", "模型")) * 0.22f;

        if (chunk.ContentKind is "abstract" or "intro" or "summary")
        {
            score -= 1.25f;
        }
        else if (chunk.ContentKind == "conclusion")
        {
            score -= 0.75f;
        }

        if (IsUnstructuredMarkdownBodyChunk(chunk))
        {
            score -= 2.1f;
        }

        if (LooksLikeFragmentedChunk(chunk.Text))
        {
            score -= 0.65f;
        }

        return score;
    }

    private static bool IsUnstructuredMarkdownBodyChunk(DocumentChunk chunk)
    {
        return chunk.FilePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && chunk.ContentKind == "body"
            && string.IsNullOrWhiteSpace(chunk.StructurePath);
    }

    private static bool LooksLikeFragmentedChunk(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith('，') || trimmed.StartsWith(',') || trimmed.StartsWith('、') || trimmed.StartsWith('。') || trimmed.StartsWith('.'))
        {
            return true;
        }

        if (trimmed.EndsWith("...", StringComparison.Ordinal) || trimmed.EndsWith('…'))
        {
            return true;
        }

        return trimmed.Length > 160
            && !trimmed.EndsWith('。')
            && !trimmed.EndsWith('！')
            && !trimmed.EndsWith('？')
            && !trimmed.EndsWith('.')
            && !trimmed.EndsWith('!')
            && !trimmed.EndsWith('?');
    }

    private static IReadOnlyList<string> BuildIntentTerms(QueryProfile queryProfile)
    {
        var terms = new List<string>();
        terms.AddRange(queryProfile.FocusTerms);
        foreach (var subject in queryProfile.CompareSubjects)
        {
            terms.Add(subject.ToLowerInvariant());
            terms.AddRange(ExtractQueryTokens(subject).Where(IsMeaningfulFocusTerm));
        }

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Where(term => !IsIntentExpansionOnlyToken(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ChunkMatchesAnyTerm(DocumentChunk chunk, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return false;
        }

        var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        return terms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static bool ChunkMatchesCompareSubjects(DocumentChunk chunk, IReadOnlyList<string> subjects)
    {
        if (subjects.Count < 2)
        {
            return false;
        }

        var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        return subjects.Take(2).All(subject => SubjectMatchesText(subject, text));
    }

    private static bool SubjectMatchesText(string subject, string lowerText)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return false;
        }

        var normalized = subject.ToLowerInvariant();
        if (lowerText.Contains(normalized, StringComparison.Ordinal))
        {
            return true;
        }

        var terms = ExtractQueryTokens(subject)
            .Where(IsMeaningfulFocusTerm)
            .Where(term => !IsIntentExpansionOnlyToken(term))
            .ToArray();
        return terms.Length > 0 && terms.Any(term => lowerText.Contains(term, StringComparison.Ordinal));
    }

    private static bool IsSummaryLeadCandidate(DocumentChunk chunk, QueryProfile queryProfile)
    {
        if (queryProfile.AvoidsEnglishMetadata && IsEnglishFieldChunk(chunk))
        {
            return false;
        }

        if (queryProfile.DisallowKeywordLikeLeadChunksForSummary && chunk.ContentKind == "keyword")
        {
            return false;
        }

        if (queryProfile.DisallowKeywordLikeLeadChunksForSummary
            && (IsEnglishTitleChunk(chunk) || IsEnglishAbstractChunk(chunk) || IsEnglishKeywordsChunk(chunk)))
        {
            return false;
        }

        if (chunk.ContentKind == "keyword")
        {
            return false;
        }

        return IsAbstractChunk(chunk) || IsOverviewChunk(chunk) || chunk.ContentKind == "conclusion";
    }

    private static bool IsSummarySupportCandidate(DocumentChunk chunk, QueryProfile queryProfile)
    {
        if (queryProfile.AvoidsEnglishMetadata && IsEnglishFieldChunk(chunk))
        {
            return false;
        }

        if (chunk.ContentKind == "keyword")
        {
            return false;
        }

        if (queryProfile.DisallowKeywordLikeLeadChunksForSummary
            && (IsEnglishTitleChunk(chunk) || IsEnglishAbstractChunk(chunk) || IsEnglishKeywordsChunk(chunk)))
        {
            return false;
        }

        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        return CountSignals(source, "数据采集", "决策控制", "执行机构", "交互显示", "workflow", "架构", "模块", "结论", "测试", "感知", "决策", "执行") > 0;
    }

    private static int GetSummaryContextPriority(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var score = 0;
        if (IsAbstractChunk(chunk))
        {
            score += 8;
        }
        if (IsOverviewChunk(chunk))
        {
            score += 7;
        }
        if (chunk.ContentKind == "conclusion")
        {
            score += 6;
        }
        if (chunk.ContentKind == "summary")
        {
            score += 4;
        }
        if (queryProfile.DisallowKeywordLikeLeadChunksForSummary && chunk.StructurePath.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        {
            score -= 4;
        }
        return score + CountSignals($"{chunk.SectionTitle} {chunk.StructurePath}", "摘要", "概述", "overview", "结论", "架构");
    }

    private static int GetSummarySupportPriority(DocumentChunk chunk)
    {
        return CountSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}",
            "数据采集", "决策控制", "执行机构", "交互显示", "感知", "决策", "执行", "workflow", "结论", "测试", "架构", "模块");
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
