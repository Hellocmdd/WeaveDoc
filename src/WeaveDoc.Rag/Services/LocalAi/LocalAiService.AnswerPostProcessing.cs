using System.Text;
using System.Text.RegularExpressions;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
    internal static bool ContainsCjk(string text)
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

    internal static bool IsEnglishFieldChunk(DocumentChunk chunk)
    {
        if (chunk.ContentKind is "englishTitle" or "englishAbstract" or "englishKeywords")
        {
            return true;
        }

        var structureText = $"{chunk.SectionTitle} {chunk.StructurePath}";
        return structureText.Contains("english", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldKeepChunkForAnswer(QueryProfile queryProfile, DocumentChunk chunk)
    {
        if (queryProfile.AvoidsEnglishMetadata && IsEnglishFieldChunk(chunk))
        {
            return false;
        }

        if (queryProfile.Intent == "metadata")
        {
            if (IsEnglishFieldChunk(chunk))
            {
                return queryProfile.RequestsEnglishMetadata || !IsChineseQuestion(queryProfile.OriginalQuestion);
            }

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

    private bool IsOffTopicOrMalformedAnswer(string answer, string question, IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            Console.Error.WriteLine("[QA-Reject] A1-empty-answer");
            return true;
        }

        var normalized = answer.Trim();
        if (normalized.Contains("我不知道", StringComparison.Ordinal)
            || normalized.Contains("当前文档未覆盖", StringComparison.Ordinal)
            || normalized.Contains("未找到相关信息", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("[QA-Reject] A2-refusal-phrase");
            return true;
        }

        if (normalized.Contains("Problem:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Constraints:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Approach:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("nums =", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("target =", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("two numbers", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("[QA-Reject] A3-code-leak");
            return true;
        }

        var requestedDocumentTitle = ExtractRequestedDocumentTitle(question);
        var meaningfulTokens = BuildRetrievalQueryTokens(question, requestedDocumentTitle)
            .Where(token => !IsAnswerRelevanceExpansionOnlyToken(token))
            .Where(IsMeaningfulFocusTerm)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var lowerAnswer = normalized.ToLowerInvariant();
        var hasSemanticSignal = meaningfulTokens.Length > 0
            && !(IsSummaryQuestion(question) && meaningfulTokens.Length <= 2)
            && meaningfulTokens.Any(token => lowerAnswer.Contains(token, StringComparison.Ordinal));

        if (!hasSemanticSignal && meaningfulTokens.Length > 0)
        {
            Console.Error.WriteLine($"[QA-Reject] A15-zero-token-overlap tokens=[{string.Join(", ", meaningfulTokens)}] q='{TruncateForLog(question)}' a='{TruncateForLog(normalized)}'");
            return true;
        }

        if (HasTitleOrAuthorBlock(normalized))
        {
            if (!hasSemanticSignal)
            {
                Console.Error.WriteLine("[QA-Reject] A4-title-author-block");
                return true;
            }
            Console.Error.WriteLine("[QA-Ignore] A4-title-author-block (semantic-signal-present)");
        }

        if (IsLikelyEnglishAbstract(normalized, question))
        {
            Console.Error.WriteLine("[QA-Reject] A5-english-abstract");
            return true;
        }

        if (ContainsUnknownBareChunkCitation(normalized, chunks))
        {
            Console.Error.WriteLine("[QA-Reject] A6-unknown-bare-citation");
            return true;
        }

        var hasCitation = CitationRegex.IsMatch(normalized);
        if (hasCitation && !ContainsOnlyKnownCitations(normalized, chunks))
        {
            if (!hasSemanticSignal)
            {
                Console.Error.WriteLine("[QA-Reject] A7-unknown-citation");
                return true;
            }
            Console.Error.WriteLine("[QA-Ignore] A7-unknown-citation (semantic-signal-present)");
        }

        if (hasCitation
            && targetFilePaths.Count > 0
            && !ContainsAnyCitationFromTargetDocument(normalized, chunks, targetFilePaths))
        {
            Console.Error.WriteLine("[QA-Reject] A8-cross-doc-citation");
            return true;
        }

        if ((question.Contains("为什么", StringComparison.Ordinal)
            || question.Contains("如何", StringComparison.Ordinal)
            || question.Contains("怎么", StringComparison.Ordinal)
            || question.Contains("总结", StringComparison.Ordinal)
            || question.Contains("概述", StringComparison.Ordinal))
            && normalized.Length < 40)
        {
            Console.Error.WriteLine($"[QA-Reject] A9-too-short-wh length={normalized.Length} q='{TruncateForLog(question)}' a='{TruncateForLog(normalized)}'");
            return true;
        }

        if (WantsDetailedAnswer(question) && normalized.Length < 120)
        {
            Console.Error.WriteLine($"[QA-Reject] A10-too-short-detailed length={normalized.Length} q='{TruncateForLog(question)}'");
            return true;
        }

        if (IsUsageQuestion(question) && IsGenericUsageAnswer(normalized))
        {
            Console.Error.WriteLine("[QA-Reject] A11-generic-usage");
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
                Console.Error.WriteLine("[QA-Reject] A12-english-required-but-missing");
                return true;
            }
        }

        if (IsModuleImplementationQuestion(question) && !HasGenericModuleImplementationAnswerShape(normalized))
        {
            Console.Error.WriteLine("[QA-Reject] A13-module-impl-shape");
            return true;
        }

        if (IsReferenceStyleAnswer(normalized))
        {
            Console.Error.WriteLine("[QA-Reject] A14-reference-style");
            return true;
        }

        return false;
    }

    private static string TruncateForLog(string text)
    {
        var normalized = text.Replace('\n', ' ').Replace('\r', ' ');
        return normalized.Length <= 120 ? normalized : normalized[..120] + "...";
    }

    internal static string NormalizeGeneratedAnswerCitations(
        string answer,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths,
        QueryProfile? queryProfile = null)
    {
        if (string.IsNullOrWhiteSpace(answer)
            || chunks.Count == 0
            || answer.Contains("我不知道", StringComparison.Ordinal)
            || answer.Contains("当前文档未覆盖", StringComparison.Ordinal)
            || answer.Contains("未找到相关信息", StringComparison.Ordinal))
        {
            return answer;
        }

        answer = ExpandKnownBareChunkCitations(answer, chunks);
        if (CitationRegex.IsMatch(answer))
        {
            return answer;
        }

        var defaultCitation = SelectDefaultCitation(chunks, targetFilePaths, queryProfile);
        if (string.IsNullOrWhiteSpace(defaultCitation))
        {
            return answer;
        }

        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);
        var scopedChunks = chunks
            .Where(item => targetFilePathSet is null || targetFilePathSet.Contains(item.FilePath))
            .ToArray();

        var lines = answer
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var builder = new StringBuilder(answer.Length + (defaultCitation.Length * Math.Max(1, lines.Length)));

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                builder.AppendLine();
                continue;
            }

            builder.Append(line);
            if (!CitationRegex.IsMatch(line))
            {
                var lineCitation = SelectCitationForAnswerLine(line, scopedChunks, queryProfile);
                builder.Append(' ');
                builder.Append(string.IsNullOrWhiteSpace(lineCitation) ? defaultCitation : lineCitation);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string SelectCitationForAnswerLine(string line, IReadOnlyList<DocumentChunk> chunks, QueryProfile? queryProfile)
    {
        if (string.IsNullOrWhiteSpace(line) || chunks.Count == 0)
        {
            return string.Empty;
        }

        var normalizedLine = NormalizeSummarySentence(CitationRegex.Replace(line, string.Empty));
        if (string.IsNullOrWhiteSpace(normalizedLine))
        {
            return string.Empty;
        }

        var lineTerms = ExtractQueryTokens(normalizedLine)
            .Where(IsMeaningfulFocusTerm)
            .Where(term => !IsIntentExpansionOnlyToken(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var bestChunk = chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreAnswerLineAgainstChunk(normalizedLine, lineTerms, chunk, queryProfile)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .FirstOrDefault();

        return bestChunk is null ? string.Empty : BuildStableCitation(bestChunk);
    }

    private static int ScoreAnswerLineAgainstChunk(string normalizedLine, IReadOnlyList<string> lineTerms, DocumentChunk chunk, QueryProfile? queryProfile)
    {
        var score = LocalAiFallbackAnswerBuilder.GetFallbackChunkScore(chunk, queryProfile);
        var retrievalText = NormalizeSummarySentence(BuildChunkRetrievalText(chunk));
        var sectionText = NormalizeSummarySentence($"{chunk.SectionTitle} {chunk.StructurePath}");

        foreach (var term in lineTerms)
        {
            var normalizedTerm = NormalizeSummarySentence(term);
            if (string.IsNullOrWhiteSpace(normalizedTerm))
            {
                continue;
            }

            if (sectionText.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                score += 4;
            }

            if (retrievalText.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                score += 2;
            }
        }

        if (normalizedLine.Length >= 8)
        {
            var overlap = lineTerms.Count(term => retrievalText.Contains(NormalizeSummarySentence(term), StringComparison.Ordinal));
            score += overlap * 2;
        }

        if (queryProfile?.Intent == "procedure")
        {
            score += CountProcedureStructureSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}");
        }

        return score;
    }

    internal static string SelectDefaultCitation(IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<string> targetFilePaths, QueryProfile? queryProfile = null)
    {
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);

        var chunk = chunks
            .Where(item => targetFilePathSet is null || targetFilePathSet.Contains(item.FilePath))
            .OrderByDescending(item => LocalAiFallbackAnswerBuilder.GetFallbackChunkScore(item, queryProfile))
            .ThenBy(item => item.Index)
            .FirstOrDefault();

        return chunk is null ? string.Empty : BuildStableCitation(chunk);
    }

    internal static string ExpandKnownBareChunkCitations(string answer, IReadOnlyList<DocumentChunk> chunks)
    {
        if (string.IsNullOrWhiteSpace(answer) || chunks.Count == 0)
        {
            return answer;
        }

        var citationsByIndex = chunks
            .GroupBy(chunk => chunk.Index + 1)
            .ToDictionary(group => group.Key, group => BuildStableCitation(group.First()));

        return BareChunkCitationRegex.Replace(answer, match =>
        {
            if (!int.TryParse(match.Groups["index"].Value, out var index))
            {
                return match.Value;
            }

            return citationsByIndex.TryGetValue(index, out var citation)
                ? citation
                : match.Value;
        });
    }

    internal static bool ContainsUnknownBareChunkCitation(string answer, IReadOnlyList<DocumentChunk> chunks)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        var knownIndexes = chunks.Select(chunk => chunk.Index + 1).ToHashSet();
        foreach (Match match in BareChunkCitationRegex.Matches(answer))
        {
            if (!int.TryParse(match.Groups["index"].Value, out var index) || !knownIndexes.Contains(index))
            {
                return true;
            }
        }

        return false;
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

    private static bool IsAnswerRelevanceExpansionOnlyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        return IsIntentExpansionOnlyToken(token)
            || token is "overview"
                or "summary"
                or "abstract"
                or "摘要"
                or "概述"
                or "引言"
                or "结论"
                or "主要内容"
                or "定义"
                or "机制"
                or "流程"
                or "步骤"
                or "模块"
                or "接口"
                or "作用"
                or "用途"
                or "功能";
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

    private static bool IsLikelyEnglishAbstract(string text, string question)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 20)
        {
            return false;
        }

        var hasChinese = text.Any(c => c >= '\u4e00' && c <= '\u9fff');
        var questionHasChinese = !string.IsNullOrWhiteSpace(question) && question.Any(c => c >= '\u4e00' && c <= '\u9fff');
        if (hasChinese)
        {
            return false;
        }

        var hasEnglish = text.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
        if (!hasEnglish)
        {
            return false;
        }

        if (questionHasChinese && !hasChinese)
        {
            return true;
        }

        var hasCnPunct = text.Contains('。') || text.Contains('，') || text.Contains('：');
        if (hasCnPunct)
        {
            return false;
        }

        var q = question ?? string.Empty;
        if ((q.Contains("作用") || q.Contains("用途") || q.Contains("功能"))
            && !(text.Contains("作用") || text.Contains("用途") || text.Contains("功能")))
        {
            return true;
        }

        var lower = text.ToLowerInvariant();
        if (lower.Contains("was designed") || lower.Contains("system based on") || lower.Contains("implemented") || lower.Contains("is proposed") || lower.Contains("is used"))
        {
            return true;
        }

        return text.Length > 40 && !hasChinese && hasEnglish;
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

        if (queryProfile.Intent is "summary" or "metadata" or "procedure" or "composition" or "explain" or "definition")
        {
            return false;
        }

        if (chunks.Count > 3 || queryProfile.PreferFallbackOverUnknown)
        {
            return false;
        }

        var topicalTokens = BuildRetrievalQueryTokens(queryProfile.OriginalQuestion, queryProfile.RequestedDocumentTitle)
            .Where(token => !IsAnswerRelevanceExpansionOnlyToken(token))
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
        if (matchedTokenCount > 0)
        {
            return false;
        }

        return !HasAnyTermInChunks(topicalTokens, scopedChunks);
    }

    private static string BuildUnknownAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var subjectTerms = ExtractQuestionSubjectTerms(queryProfile.OriginalQuestion);
        if (subjectTerms.Count == 0)
        {
            return "未找到相关内容。";
        }

        var termList = string.Join("、", subjectTerms.Take(6));
        var citation = SelectDefaultCitation(chunks, targetFilePaths, queryProfile);
        if (string.IsNullOrWhiteSpace(citation))
        {
            return $"未找到关于{termList}的相关内容。";
        }

        return $"未找到关于{termList}的相关内容。 {citation}";
    }

    private static bool IsAbstractChunk(DocumentChunk chunk)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        return source.Contains("abstract", StringComparison.OrdinalIgnoreCase)
            || source.Contains("摘要", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOverviewChunk(DocumentChunk chunk)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        return source.Contains("overview", StringComparison.OrdinalIgnoreCase)
            || source.Contains("概述", StringComparison.OrdinalIgnoreCase)
            || source.Contains("总体", StringComparison.OrdinalIgnoreCase);
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

        var startsLikeReferenceNumber = trimmed.Length > 0 && (trimmed.StartsWith("[", StringComparison.Ordinal) || char.IsDigit(trimmed[0]));
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

    internal static string BuildStableCitation(DocumentChunk chunk)
    {
        return BuildStableCitation(chunk.FilePath, string.IsNullOrWhiteSpace(chunk.StructurePath) ? chunk.SectionTitle : chunk.StructurePath, chunk.Index);
    }

    internal static string BuildStableCitation(string filePath, string sectionTitle, int chunkIndex)
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
}
