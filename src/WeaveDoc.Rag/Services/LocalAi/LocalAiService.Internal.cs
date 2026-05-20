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
    internal static string BuildChunkRetrievalText(DocumentChunk chunk)
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
        foreach (var token in LocalAiQuestionAnalyzer.EnumerateQueryTokens(text))
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
        if (left.Length != right.Length)
        {
            throw new ArgumentException($"Embedding dimension mismatch: {left.Length} vs {right.Length}");
        }

        var length = left.Length;
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
        return LocalAiQuestionAnalyzer.NormalizeQuestionForRetrieval(question, history);
    }

    private static string StripQuestionBoilerplate(string question)
    {
        return LocalAiQuestionAnalyzer.StripQuestionBoilerplate(question);
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
        return QueryUnderstandingService.BuildProfile(question);
    }

    internal static bool AvoidsEnglishMetadata(string question)
    {
        return LocalAiQuestionAnalyzer.AvoidsEnglishMetadata(question);
    }

    internal static bool RequestsEnglishMetadata(string question)
    {
        return LocalAiQuestionAnalyzer.RequestsEnglishMetadata(question);
    }

    internal static bool DisallowKeywordLikeLeadChunksForSummary(string question)
    {
        return LocalAiQuestionAnalyzer.DisallowKeywordLikeLeadChunksForSummary(question);
    }

    internal static bool PreferFallbackOverUnknown(string question)
    {
        return LocalAiQuestionAnalyzer.PreferFallbackOverUnknown(question);
    }

    internal static IReadOnlyList<string> ExtractCompareSubjects(string question)
    {
        return LocalAiQuestionAnalyzer.ExtractCompareSubjects(question);
    }

    internal static bool IsMeaningfulFocusTerm(string token)
    {
        return LocalAiQuestionAnalyzer.IsMeaningfulFocusTerm(token);
    }

    internal static IReadOnlyList<string> BuildRetrievalQueryTokens(string question, string? requestedDocumentTitle = null)
    {
        return LocalAiQuestionAnalyzer.BuildRetrievalQueryTokens(question, requestedDocumentTitle);
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

    internal static string? ExtractRequestedDocumentTitle(string question)
    {
        return LocalAiQuestionAnalyzer.ExtractRequestedDocumentTitle(question);
    }


    private static int ComputeRequestedDocumentMatchScore(string normalizedTarget, string? candidate)
    {
        return LocalAiQuestionAnalyzer.ComputeRequestedDocumentMatchScore(normalizedTarget, candidate);
    }

    private static string NormalizeLookupText(string? value)
    {
        return LocalAiQuestionAnalyzer.NormalizeLookupText(value);
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

    internal static string DetectIntent(string question)
    {
        return LocalAiQuestionAnalyzer.DetectIntent(question);
    }

    internal static IReadOnlyList<string> ExtractQueryTokens(string text)
    {
        return LocalAiQuestionAnalyzer.ExtractQueryTokens(text);
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
            "abstract" => 1.05f,
            "intro" or "conclusion" => 0.75f,
            "body" or "" => 1f,
            "keyword" => 0.9f,
            "title" => 0.7f,
            "metadata" => 0.15f,
            "reference" or "noise" => 0.05f,
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

        if (chunk.ContentKind is "metadata" or "reference" or "noise")
        {
            penalty += chunk.ContentKind == "metadata" ? 0.28f : 0.38f;
            if (text.Length < 120)
            {
                penalty += 0.08f;
            }
        }

        if (chunk.ContentKind == "keyword")
        {
            penalty -= 0.04f;
        }

        if (chunk.ContentKind is "summary" or "abstract")
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
            "abstract" => 1.05f,
            "intro" or "conclusion" => 0.8f,
            "keyword" => 1.2f,
            "metadata" => 0.55f,
            "reference" or "noise" => 0.1f,
            _ => 1f
        };
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

    private static bool TryBuildLocalFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths,
        out string answer)
    {
        return LocalAiFallbackAnswerBuilder.TryBuild(queryProfile, chunks, targetFilePaths, out answer);
    }

    internal static string BuildLocalSummaryFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        return LocalAiFallbackAnswerBuilder.BuildSummaryFallbackAnswer(queryProfile, chunks, targetFilePaths);
    }

    private static DocumentChunk[] FilterChunksByTargetFilePaths(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);

        return chunks
            .Where(chunk => targetFilePathSet is null || targetFilePathSet.Contains(chunk.FilePath))
            .ToArray();
    }

    private static void AppendFallbackBullet(StringBuilder builder, string label, DocumentChunk? chunk, string fallbackText)
    {
        AppendFallbackBullet(builder, label, chunk, null, [], fallbackText);
    }

    private static void AppendFallbackBullet(
        StringBuilder builder,
        string label,
        DocumentChunk? chunk,
        QueryProfile? queryProfile,
        IReadOnlyList<string> signals,
        string fallbackText)
    {
        if (chunk is null)
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append("：");

        var sentence = GetBestEvidenceSentence(chunk, queryProfile, signals);
        if (string.IsNullOrWhiteSpace(sentence))
        {
            sentence = fallbackText;
        }

        if (sentence.Length > 150)
        {
            sentence = sentence[..150].TrimEnd('，', ',', '；', ';') + "。";
        }

        builder.Append(sentence.TrimEnd('。'));
        builder.Append("。 ");
        builder.Append(BuildStableCitation(chunk));
        builder.AppendLine();
    }

    private static bool IsModuleImplementationQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsModuleImplementationQuestion(question);
    }

    internal static int CountGenericAnswerEntities(string text)
    {
        return LocalAiQuestionAnalyzer.CountGenericAnswerEntities(text);
    }

    private static bool IsSummaryQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsSummaryQuestion(question);
    }

    internal static bool WantsDetailedAnswer(string question)
    {
        return LocalAiQuestionAnalyzer.WantsDetailedAnswer(question);
    }

    internal static bool IsFollowUpExpansionRequest(string question)
    {
        return LocalAiQuestionAnalyzer.IsFollowUpExpansionRequest(question);
    }

    private static bool IsModuleQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsModuleQuestion(question);
    }

    private static bool ShouldAugmentWithPreviousQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.ShouldAugmentWithPreviousQuestion(question);
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
                    ? "如果是在总结论文或文章，先用 1-2 句概括主题与核心结论，再按“研究对象/问题 -> 方法或系统设计 -> 关键发现或创新 -> 局限/启示”的主线自然展开；优先综合 abstract、overview、结论等块，避免把相近内容拆成很多并列短句。"
                    : "如果是在总结论文或文章，先用 1 句概括主题与核心结论，再围绕一条主线自然展开 2-3 个层次；优先综合 abstract、overview、结论等块，避免机械罗列多个并列要点。";
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
                ? "如果是在总结文档，先概括主题与主旨，再围绕 2-3 个核心方面自然展开；优先综合 abstract、overview、结论和正文核心段落，保留关键模块名、方法名或阶段名，但不要把相近内容拆成很多碎片化短句。"
                : "如果是在总结文档，先概括主旨，再用 2-3 句或 2-3 个层次串起核心内容；优先综合 abstract、overview、结论和正文核心段落，不要只复述一句原文，也不要机械罗列多个并列短语。";
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
            return "如果是在做对比，直接陈述两边的具体差异，不要用\"可以按...比较\"等元描述开头。先一句话点明核心差异，再按 2-3 个维度分别展开两边在该维度上的表现；每个维度下必须同时覆盖双边。不要只解释其中一边。";
        }

        if (queryProfile.Intent == "explain")
        {
            return "如果是在解释原因或机制，先给结论，再说明触发条件、作用机制和结果影响。";
        }

        if (queryProfile.Intent == "definition")
        {
            return "如果是在问概念或对象是什么，先给出定义或身份，再补充它的职责、特征或在系统中的位置。";
        }

        if (queryProfile.Intent == "procedure")
        {
            return queryProfile.WantsDetailedAnswer
                ? "如果是在问流程或实现方式，按\"核心目标 → 关键机制 → 具体步骤 → 执行方式 → 效果\"的结构组织。直接陈述每个环节中系统实际使用的算法、组件、参数和结果，不要用\"可以按...链路来理解\"等元描述开头。优先使用描述系统核心控制或处理逻辑的上下文块，不要偏到通信接口配置、显示模块等外围环节。若上下文同时包含多个可能的流程链路，优先选择与问题核心动词直接相关的那一条。"
                : "如果是在问流程或实现方式，按\"核心目标 → 关键机制 → 具体步骤 → 结果\"的结构组织。直接陈述每个环节的系统实际实现，不要用\"可以按...链路来理解\"等元描述开头。优先围绕系统核心功能展开，不要偏到通信配置等辅助环节。";
        }

        if (queryProfile.Intent == "module_implementation")
        {
            return queryProfile.WantsDetailedAnswer
                ? "如果是在问某个模块怎么做的，先点明该模块的目标和通信链路，再按”数据/状态从哪里来 -> 通过什么接口或协议传输 -> 在哪里显示或由谁下发控制 -> 系统如何执行或由哪一层完成”的顺序展开；不要泛化成整个系统流程。"
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
                return "如果问题是在问论文或文档主要写什么，优先综合 abstract、overview、结论和正文中的核心段落，先概括研究目标，再沿着“目标 -> 方法/系统 -> 结果/意义”自然展开；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，但不要把它们逐条堆成清单。";
            }

            return "如果问题是在问论文或文档主要写什么，优先综合 abstract、overview、结论和正文中的核心段落，先概括主旨，再用一条清晰主线串起核心内容点；如果原文明确列出了模块名、层级名或组成项，尽量保留原始术语，但不要机械罗列。";
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
            return "对比类问题必须点出核心差异维度（如适用阶段、触发条件、处理方式、输出效果），同时保留两边的原始术语、阶段名或模块名；每项对比必须覆盖双边。直接陈述差异内容，不要用\"可以按...比较\"等元描述开头。";
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
                ? "流程类问题优先按“输入或前提 -> 核心步骤 -> 输出或结果”展开，聚焦系统核心功能链路；如果原文出现模块名、算法名、协议名或控制信号，尽量直接保留。"
                : "流程类问题优先点出前提、关键步骤和结果，聚焦系统核心功能链路；如果原文出现模块名、算法名、协议名或控制信号，尽量直接保留。";
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

    private static IReadOnlyList<string> ExtractQuestionSubjectTerms(string question)
    {
        return LocalAiQuestionAnalyzer.ExtractQuestionSubjectTerms(question);
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

    internal static bool IsIntentExpansionOnlyToken(string token)
    {
        return LocalAiQuestionAnalyzer.IsIntentExpansionOnlyToken(token);
    }

    private static bool IsUsageQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsUsageQuestion(question);
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

    private static DocumentChunk? FindRepresentativeChunk(
        IReadOnlyList<DocumentChunk> chunks,
        QueryProfile queryProfile,
        IReadOnlyList<string> signals)
    {
        var focusTerms = queryProfile.FocusTerms
            .Where(term => !IsIntentExpansionOnlyToken(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreChunkForTerms(chunk, signals)
                    + ScoreChunkForTerms(chunk, focusTerms)
                    + (chunk.ContentKind == "body" ? 2 : 0)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> BuildSubjectTerms(string subject)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(subject))
        {
            terms.Add(subject.ToLowerInvariant());
            terms.AddRange(ExtractQueryTokens(subject)
                .Where(IsMeaningfulFocusTerm)
                .Where(term => !IsIntentExpansionOnlyToken(term)));
        }

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ChunkContainsAnyTerm(DocumentChunk chunk, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return false;
        }

        var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        return terms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static int ScoreChunkForTerms(DocumentChunk chunk, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var section = $"{chunk.SectionTitle} {chunk.StructurePath}".ToLowerInvariant();
        var text = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        var score = 0;
        foreach (var term in terms.Where(term => !string.IsNullOrWhiteSpace(term)))
        {
            if (section.Contains(term, StringComparison.Ordinal))
            {
                score += 3;
            }

            if (text.Contains(term, StringComparison.Ordinal))
            {
                score += 1;
            }
        }

        return score;
    }

    private static bool ContainsAny(string text, params string[] signals)
    {
        return signals.Any(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    private static string GetBestEvidenceSentence(
        DocumentChunk chunk,
        QueryProfile? queryProfile,
        IReadOnlyList<string> signals)
    {
        var focusTerms = queryProfile?.FocusTerms
            .Where(term => !IsIntentExpansionOnlyToken(term))
            .ToArray() ?? [];
        var sentences = SentenceSplitRegex.Split(chunk.Text)
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(8)
            .ToArray();

        if (sentences.Length == 0)
        {
            return string.Empty;
        }

        return sentences
            .Select(sentence => new
            {
                Sentence = sentence,
                Score = CountSignals(sentence, signals.ToArray())
                    + CountQueryTokenHits(sentence, focusTerms)
                    + CountMethodSignals(sentence)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Sentence.Length)
            .Select(item => item.Sentence)
            .FirstOrDefault() ?? sentences[0];
    }

    private static string BuildFallbackLabel(DocumentChunk chunk)
    {
        var candidates = new[]
        {
            chunk.SectionTitle,
            chunk.StructurePath.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty,
            chunk.ContentKind
        };

        foreach (var candidate in candidates)
        {
            var cleaned = CleanStructuredSentence(candidate)
                .Trim('，', '。', '；', '：', ':', '/', '\\', '|', ' ');
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned.Length <= 24 ? cleaned : cleaned[..24].TrimEnd();
            }
        }

        return "组成项";
    }

    private static bool IsCompositionQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsCompositionQuestion(question);
    }

    private static bool IsProcedureQuestion(string question)
    {
        return LocalAiQuestionAnalyzer.IsProcedureQuestion(question);
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

    internal static IEnumerable<string> GetCleanSentences(string text, int takeCount)
    {
        return SentenceSplitRegex.Split(text)
            .Select(CleanStructuredSentence)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(takeCount);
    }

    internal static string CleanStructuredSentence(string sentence)
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

    internal static bool IsParameterHeavySentence(string sentence)
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

}
