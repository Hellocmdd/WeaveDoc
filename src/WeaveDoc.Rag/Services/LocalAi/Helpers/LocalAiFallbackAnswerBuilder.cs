using System.Text;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

internal static class LocalAiFallbackAnswerBuilder
{
    private static readonly string[] CompareSignals = ["区别", "不同", "差异", "相比", "而", "则", "一方面", "另一方面"];
    private static readonly string[] CompositionContextSignals = ["硬件", "软件", "架构", "技术栈", "前后端", "模块", "组成", "构成", "配置", "条件", "环境", "接口", "参数", "依赖"];

    public static bool TryBuild(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths,
        out string answer)
    {
        answer = string.Empty;
        if (chunks.Count == 0)
        {
            return false;
        }

        if (queryProfile.Intent == "summary")
        {
            answer = BuildSummaryFallbackAnswer(queryProfile, chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        if (queryProfile.Intent == "metadata")
        {
            answer = BuildMetadataFallbackAnswer(chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        if (queryProfile.Intent == "procedure")
        {
            answer = BuildProcedureFallbackAnswer(queryProfile, chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        if (queryProfile.Intent == "compare")
        {
            answer = BuildCompareFallbackAnswer(queryProfile, chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        if (queryProfile.Intent == "composition")
        {
            answer = BuildCompositionFallbackAnswer(queryProfile, chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        if (queryProfile.Intent == "explain")
        {
            answer = BuildExplainFallbackAnswer(queryProfile, chunks, targetFilePaths);
            return !string.IsNullOrWhiteSpace(answer);
        }

        return false;
    }

    public static string BuildSummaryFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);

        var scopedChunks = chunks
            .Where(chunk => targetFilePathSet is null || targetFilePathSet.Contains(chunk.FilePath))
            .Where(IsSummaryFallbackCandidate)
            .Where(chunk => !LocalAiService.IsEnglishFieldChunk(chunk))
            .Where(chunk => LocalAiService.ContainsCjk(chunk.Text) || !LocalAiService.ContainsCjk(queryProfile.OriginalQuestion))
            .ToArray();

        if (scopedChunks.Length == 0)
        {
            return string.Empty;
        }

        var leadChunk = scopedChunks
            .Where(IsSummaryFallbackLeadCandidate)
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = GetSummaryFallbackLeadScore(chunk, queryProfile)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .FirstOrDefault()
            ?? scopedChunks
                .OrderByDescending(chunk => GetFallbackChunkScore(chunk, queryProfile))
                .ThenBy(chunk => chunk.Index)
                .First();

        var supportChunks = scopedChunks
            .Where(chunk => chunk.Index != leadChunk.Index || !string.Equals(chunk.FilePath, leadChunk.FilePath, StringComparison.OrdinalIgnoreCase))
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = GetSummaryFallbackSupportScore(chunk, queryProfile)
                    + CountQueryTokenHits(LocalAiService.BuildChunkRetrievalText(chunk), queryProfile.FocusTerms)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .Take(4)
            .ToArray();

        if (supportChunks.Length < 3)
        {
            supportChunks = supportChunks
                .Concat(scopedChunks.Where(chunk => chunk.Index != leadChunk.Index || !string.Equals(chunk.FilePath, leadChunk.FilePath, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(chunk => $"{chunk.FilePath}#{chunk.Index}")
                .Take(4)
                .ToArray();
        }

        var leadSummary = BuildSummaryLeadText(leadChunk);
        if (string.IsNullOrWhiteSpace(leadSummary))
        {
            leadSummary = LocalAiService.CleanStructuredSentence(leadChunk.Text);
        }

        if (leadSummary.Length > 260)
        {
            leadSummary = leadSummary[..260].TrimEnd('，', ',', '；', ';') + "。";
        }

        var builder = new StringBuilder();
        var title = string.IsNullOrWhiteSpace(leadChunk.DocumentTitle) ? "这篇文档" : $"《{leadChunk.DocumentTitle}》";
        builder.Append(title);
        builder.Append("主要讲的是");
        builder.Append(leadSummary.TrimEnd('。'));
        builder.Append("。 ");
        builder.Append(LocalAiService.BuildStableCitation(leadChunk));

        if (supportChunks.Length > 0)
        {
            builder.AppendLine();
            foreach (var chunk in supportChunks)
            {
                var sentence = BuildSummarySupportSentence(chunk, queryProfile);
                if (string.IsNullOrWhiteSpace(sentence))
                {
                    continue;
                }

                if (sentence.Length > 150)
                {
                    sentence = sentence[..150].TrimEnd('，', ',', '；', ';') + "。";
                }

                builder.Append("- ");
                builder.Append(BuildSummaryFallbackLabel(chunk));
                builder.Append("：");
                builder.Append(sentence.TrimEnd('。'));
                builder.Append("。 ");
                builder.Append(LocalAiService.BuildStableCitation(chunk));
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildProcedureFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var scopedChunks = FilterChunksByTargetFilePaths(chunks, targetFilePaths);
        if (scopedChunks.Length == 0)
        {
            return string.Empty;
        }

        var inputSignals = new[] { "输入", "前提", "条件", "数据", "参数", "采集", "获取", "来源", "准备" };
        var processSignals = new[] { "判断", "计算", "分析", "处理", "算法", "规则", "策略", "生成", "调节", "控制", "匹配", "根据", "结合" };
        var actionSignals = new[] { "执行", "输出", "驱动", "调用", "写入", "发送", "上传", "下发", "展示", "显示", "保存", "触发", "连接" };
        var resultSignals = new[] { "结果", "效果", "最终", "实现", "完成", "提升", "降低", "减少", "控制在", "验证", "测试", "稳定" };

        var inputChunk = FindRepresentativeChunk(scopedChunks, queryProfile, inputSignals);
        var processChunk = FindRepresentativeChunk(scopedChunks, queryProfile, processSignals);
        var actionChunk = FindRepresentativeChunk(scopedChunks, queryProfile, actionSignals);
        var resultChunk = FindRepresentativeChunk(scopedChunks, queryProfile, resultSignals);

        var ordered = new[]
            { inputChunk, processChunk, actionChunk, resultChunk }
            .Where(chunk => chunk is not null)
            .Cast<DocumentChunk>()
            .DistinctBy(chunk => $"{chunk.FilePath}#{chunk.Index}")
            .ToArray();

        if (ordered.Length < 3)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append(LocalAiService.BuildStableCitation(ordered[0]));
        builder.AppendLine();

        AppendFallbackBullet(builder, "输入/前提", inputChunk, queryProfile, inputSignals, "上下文给出了该流程的输入、前提或准备条件。");
        AppendFallbackBullet(builder, "判断或处理", processChunk, queryProfile, processSignals, "上下文给出了该流程中的判断、处理或策略生成方式。");
        AppendFallbackBullet(builder, "执行动作", actionChunk, queryProfile, actionSignals, "上下文给出了该流程如何转化为执行、输出或外部动作。");
        AppendFallbackBullet(builder, "结果/效果", resultChunk, queryProfile, resultSignals, "上下文给出了该流程最终形成的结果、效果或验证信息。");
        return builder.ToString().TrimEnd();
    }

    private static string BuildCompareFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var scopedChunks = FilterChunksByTargetFilePaths(chunks, targetFilePaths);
        if (scopedChunks.Length == 0 || queryProfile.CompareSubjects.Count < 2)
        {
            return string.Empty;
        }

        var leftSubject = queryProfile.CompareSubjects[0];
        var rightSubject = queryProfile.CompareSubjects[1];
        var leftTerms = BuildSubjectTerms(leftSubject);
        var rightTerms = BuildSubjectTerms(rightSubject);

        var jointChunk = scopedChunks
            .Where(chunk => ChunkContainsAnyTerm(chunk, leftTerms) && ChunkContainsAnyTerm(chunk, rightTerms))
            .OrderByDescending(chunk => ScoreChunkForTerms(chunk, leftTerms.Concat(rightTerms).ToArray()))
            .ThenBy(chunk => chunk.Index)
            .FirstOrDefault();
        var leftChunk = jointChunk ?? FindRepresentativeChunk(scopedChunks, queryProfile, leftTerms);
        var rightChunk = jointChunk ?? FindRepresentativeChunk(scopedChunks, queryProfile, rightTerms);

        if (leftChunk is null || rightChunk is null)
        {
            return string.Empty;
        }

        var evidence = new[] { leftChunk, rightChunk, jointChunk }
            .Where(chunk => chunk is not null)
            .Cast<DocumentChunk>()
            .DistinctBy(chunk => $"{chunk.FilePath}#{chunk.Index}")
            .ToArray();
        if (evidence.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("二者的区别可以按上下文中的对应证据分别比较。 ");
        builder.Append(LocalAiService.BuildStableCitation(evidence[0]));
        builder.AppendLine();
        AppendFallbackBullet(builder, leftSubject, leftChunk, queryProfile, leftTerms, "上下文给出了该对象的特点或作用方式。");
        AppendFallbackBullet(builder, rightSubject, rightChunk, queryProfile, rightTerms, "上下文给出了该对象的特点或作用方式。");

        var combinedSignals = CompareSignals.Concat(leftTerms).Concat(rightTerms).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var contrastChunk = jointChunk
            ?? FindRepresentativeChunk(scopedChunks, queryProfile, combinedSignals)
            ?? evidence[0];
        AppendFallbackBullet(builder, "差异归纳", contrastChunk, queryProfile, combinedSignals, "对比时应同时看两边对象在条件、处理方式和输出结果上的差别。");
        return builder.ToString().TrimEnd();
    }

    private static string BuildCompositionFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var scopedChunks = FilterChunksByTargetFilePaths(chunks, targetFilePaths);
        if (scopedChunks.Length == 0)
        {
            return string.Empty;
        }

        var compositionTerms = CompositionContextSignals
            .Concat(queryProfile.FocusTerms)
            .Where(term => !LocalAiService.IsIntentExpansionOnlyToken(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var availableChunks = scopedChunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreChunkForTerms(chunk, compositionTerms)
                    + CountCompositionStructureSignals($"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}")
                    + LocalAiService.CountGenericAnswerEntities(chunk.Text)
                    + (chunk.ContentKind == "body" ? 2 : 0)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Chunk.Index)
            .Select(item => item.Chunk)
            .DistinctBy(chunk => $"{chunk.FilePath}#{chunk.Index}")
            .Take(5)
            .ToArray();

        if (availableChunks.Length < 3)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("这类组成/配置问题可以按上下文中的关键组成项归纳。 ");
        builder.Append(LocalAiService.BuildStableCitation(availableChunks[0]));
        builder.AppendLine();
        foreach (var chunk in availableChunks)
        {
            AppendFallbackBullet(builder, BuildFallbackLabel(chunk), chunk, queryProfile, compositionTerms, "上下文给出了一个组成项、配置项或模块职责。");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildExplainFallbackAnswer(
        QueryProfile queryProfile,
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var scopedChunks = FilterChunksByTargetFilePaths(chunks, targetFilePaths);
        if (scopedChunks.Length == 0)
        {
            return string.Empty;
        }

        var reasonSignals = new[] { "原因", "由于", "因为", "为了", "前提", "条件", "影响", "需要" };
        var mechanismSignals = new[] { "机制", "通过", "结合", "根据", "动态", "调整", "处理", "策略", "模型", "规则" };
        var impactSignals = new[] { "结果", "影响", "因此", "从而", "避免", "提升", "降低", "减少", "效果", "稳定" };

        var reasonChunk = FindRepresentativeChunk(scopedChunks, queryProfile, reasonSignals);
        var mechanismChunk = FindRepresentativeChunk(scopedChunks, queryProfile, mechanismSignals);
        var impactChunk = FindRepresentativeChunk(scopedChunks, queryProfile, impactSignals);

        var ordered = new[]
            { reasonChunk, mechanismChunk, impactChunk }
            .Where(chunk => chunk is not null)
            .Cast<DocumentChunk>()
            .DistinctBy(chunk => $"{chunk.FilePath}#{chunk.Index}")
            .ToArray();

        if (ordered.Length < 2)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("这个问题可以按“为什么需要 -> 如何起作用 -> 带来什么影响”来解释。 ");
        builder.Append(LocalAiService.BuildStableCitation(ordered[0]));
        builder.AppendLine();
        AppendFallbackBullet(builder, "为什么需要", reasonChunk, queryProfile, reasonSignals, "上下文说明了该机制或做法出现的背景、条件或原因。");
        AppendFallbackBullet(builder, "作用机制", mechanismChunk, queryProfile, mechanismSignals, "上下文说明了它如何通过规则、模型、流程或策略发挥作用。");
        AppendFallbackBullet(builder, "结果影响", impactChunk, queryProfile, impactSignals, "上下文说明了该机制带来的结果、效果或风险控制。");
        return builder.ToString().TrimEnd();
    }

    private static string BuildMetadataFallbackAnswer(
        IReadOnlyList<DocumentChunk> chunks,
        IReadOnlyList<string> targetFilePaths)
    {
        var targetFilePathSet = targetFilePaths.Count == 0
            ? null
            : new HashSet<string>(targetFilePaths, StringComparer.OrdinalIgnoreCase);

        var scopedChunks = chunks
            .Where(chunk => targetFilePathSet is null || targetFilePathSet.Contains(chunk.FilePath))
            .ToArray();

        var englishTitleChunk = scopedChunks
            .FirstOrDefault(chunk => chunk.ContentKind == "englishTitle"
                || chunk.SectionTitle.Contains("englishTitle", StringComparison.OrdinalIgnoreCase));
        var englishAbstractChunk = scopedChunks
            .FirstOrDefault(chunk => chunk.ContentKind == "englishAbstract"
                || chunk.SectionTitle.Contains("englishAbstract", StringComparison.OrdinalIgnoreCase));
        var englishKeywordsChunk = scopedChunks
            .FirstOrDefault(chunk => chunk.ContentKind == "englishKeywords"
                || chunk.SectionTitle.Contains("englishKeywords", StringComparison.OrdinalIgnoreCase));

        if (englishTitleChunk is null && englishAbstractChunk is null && englishKeywordsChunk is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("以下是该 JSON 文档的英文元数据：");
        builder.AppendLine();

        if (englishTitleChunk is not null)
        {
            builder.Append("英文标题: ");
            builder.Append(englishTitleChunk.Text.Trim());
            builder.Append(' ');
            builder.Append(LocalAiService.BuildStableCitation(englishTitleChunk));
            builder.AppendLine();
            builder.AppendLine();
        }

        if (englishAbstractChunk is not null)
        {
            builder.Append("英文摘要: ");
            builder.Append(englishAbstractChunk.Text.Trim());
            builder.Append(' ');
            builder.Append(LocalAiService.BuildStableCitation(englishAbstractChunk));
            builder.AppendLine();
            builder.AppendLine();
        }

        if (englishKeywordsChunk is not null)
        {
            builder.Append("英文关键词: ");
            builder.Append(englishKeywordsChunk.Text.Trim());
            builder.Append(' ');
            builder.Append(LocalAiService.BuildStableCitation(englishKeywordsChunk));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSummaryLeadText(DocumentChunk chunk)
    {
        var sentences = LocalAiService.GetCleanSentences(chunk.Text, 3)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToArray();

        if (sentences.Length == 0)
        {
            return string.Empty;
        }

        if (!IsAbstractChunk(chunk))
        {
            return sentences[0];
        }

        return string.Join(
            string.Empty,
            sentences.Take(2).Select(sentence => sentence.EndsWith('。') ? sentence : sentence + "。"));
    }

    private static bool IsSummaryFallbackLeadCandidate(DocumentChunk chunk)
    {
        return (IsAbstractChunk(chunk) || IsOverviewChunk(chunk) || chunk.ContentKind is "summary" or "conclusion")
            && !LocalAiService.IsEnglishFieldChunk(chunk)
            && LocalAiService.ContainsCjk(chunk.Text);
    }

    private static int GetSummaryFallbackLeadScore(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        var score = GetFallbackChunkScore(chunk, queryProfile);
        if (IsAbstractChunk(chunk))
        {
            score += 10;
        }

        if (IsOverviewChunk(chunk))
        {
            score += 8;
        }

        if (chunk.ContentKind == "conclusion")
        {
            score += 5;
        }

        score += CountSummarySignals(source);
        return score;
    }

    private static int GetSummaryFallbackSupportScore(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        var score = CountSummarySignals(source)
            + CountArchitectureSignals(source)
            + CountMethodSignals(source)
            + LocalAiService.CountGenericAnswerEntities(chunk.Text);

        if (chunk.ContentKind == "body")
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            score += 3;
        }

        if (IsAbstractChunk(chunk) || IsOverviewChunk(chunk))
        {
            score -= 2;
        }

        if (LocalAiService.IsEnglishFieldChunk(chunk) || chunk.ContentKind == "keyword")
        {
            score -= 10;
        }

        return score;
    }

    private static string BuildSummaryFallbackLabel(DocumentChunk chunk)
    {
        var source = $"{chunk.SectionTitle} {chunk.StructurePath} {chunk.Text}";
        if (CountSignals(source, "目标", "问题", "面向", "针对", "背景") > 0)
        {
            return "目标";
        }

        if (CountSignals(source, "系统", "架构", "模块", "组成", "层", "平台") > 0)
        {
            return "系统设计";
        }

        if (CountSignals(source, "方法", "算法", "模型", "机制", "策略", "控制", "流程") > 0)
        {
            return "控制方法";
        }

        if (CountSignals(source, "显示", "交互", "接口", "通信", "上传", "下发", "执行", "驱动") > 0)
        {
            return "模块流程";
        }

        if (CountSignals(source, "结果", "效果", "测试", "验证", "提升", "降低", "减少", "评估", "结论") > 0)
        {
            return "效果";
        }

        return BuildFallbackLabel(chunk);
    }

    private static string BuildSummarySupportSentence(DocumentChunk chunk, QueryProfile queryProfile)
    {
        var signals = new[]
        {
            "目标", "问题", "系统", "架构", "模块", "方法", "算法", "机制", "策略", "控制",
            "流程", "执行", "驱动", "显示", "交互", "结果", "效果", "测试", "验证", "提升"
        };

        return GetBestEvidenceSentence(chunk, queryProfile, signals);
    }

    private static bool IsSummaryFallbackCandidate(DocumentChunk chunk)
    {
        if (chunk.ContentKind is "reference" or "noise" or "title" or "keyword")
        {
            return false;
        }

        if (LocalAiService.IsEnglishFieldChunk(chunk))
        {
            return false;
        }

        if (chunk.ContentKind != "metadata")
        {
            return true;
        }

        return IsAbstractChunk(chunk) || IsOverviewChunk(chunk);
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

    internal static int GetFallbackChunkScore(DocumentChunk chunk, QueryProfile? queryProfile = null)
    {
        var isSummaryFallback = queryProfile?.Intent == "summary";
        var score = isSummaryFallback ? GetSummaryChunkBias(chunk) : 0;
        score += chunk.ContentKind switch
        {
            "abstract" => isSummaryFallback ? 5 : 0,
            "summary" => isSummaryFallback ? 7 : 1,
            "conclusion" => isSummaryFallback ? 6 : 2,
            "intro" => isSummaryFallback ? 4 : 1,
            "body" => 2,
            _ => 0
        };

        if (IsAbstractChunk(chunk))
        {
            score += isSummaryFallback ? 1 : -2;
        }

        if (IsOverviewChunk(chunk))
        {
            score += isSummaryFallback ? 5 : 1;
        }

        if (LocalAiService.ContainsCjk(chunk.Text))
        {
            score += 2;
        }

        if (LocalAiService.IsParameterHeavySentence(chunk.Text))
        {
            score -= 3;
        }

        if (queryProfile is not null)
        {
            score += GetIntentChunkBias(chunk, queryProfile);
        }

        return score;
    }

    private static int GetIntentChunkBias(DocumentChunk chunk, QueryProfile queryProfile)
    {
        switch (queryProfile.Intent)
        {
            case "summary":
                var summaryBias = 0;
                if (IsAbstractChunk(chunk))
                {
                    summaryBias += 5;
                }
                if (chunk.ContentKind == "summary")
                {
                    summaryBias += 2;
                }
                return summaryBias;

            case "definition":
            case "module_implementation":
            case "procedure":
            case "compare":
                var bodyBias = 0;
                if (IsAbstractChunk(chunk))
                {
                    bodyBias -= 2;
                }
                if (chunk.ContentKind == "body")
                {
                    bodyBias += 4;
                    if (queryProfile.FocusTerms.Count > 0)
                    {
                        var sectionLower = chunk.SectionTitle.ToLowerInvariant();
                        var retrievalText = LocalAiService.BuildChunkRetrievalText(chunk).ToLowerInvariant();
                        foreach (var term in queryProfile.FocusTerms)
                        {
                            if (sectionLower.Contains(term, StringComparison.OrdinalIgnoreCase))
                            {
                                bodyBias += 5;
                            }

                            if (retrievalText.Contains(term, StringComparison.Ordinal))
                            {
                                bodyBias += 2;
                            }
                        }
                    }
                }
                return bodyBias;

            case "metadata":
                if (queryProfile.RequestsEnglishMetadata
                    && chunk.ContentKind is "englishTitle" or "englishAbstract" or "englishKeywords")
                {
                    return 8;
                }

                if (chunk.ContentKind is "abstract" or "englishTitle" or "englishAbstract" or "englishKeywords"
                    || IsAbstractChunk(chunk))
                {
                    return 3;
                }
                return 0;

            default:
                return 0;
        }
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
        builder.Append(LocalAiService.BuildStableCitation(chunk));
        builder.AppendLine();
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
            .Where(term => !LocalAiService.IsIntentExpansionOnlyToken(term))
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
            terms.AddRange(LocalAiService.ExtractQueryTokens(subject)
                .Where(LocalAiService.IsMeaningfulFocusTerm)
                .Where(term => !LocalAiService.IsIntentExpansionOnlyToken(term)));
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

        var text = LocalAiService.BuildChunkRetrievalText(chunk).ToLowerInvariant();
        return terms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    private static int ScoreChunkForTerms(DocumentChunk chunk, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var section = $"{chunk.SectionTitle} {chunk.StructurePath}".ToLowerInvariant();
        var text = LocalAiService.BuildChunkRetrievalText(chunk).ToLowerInvariant();
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

    private static string GetBestEvidenceSentence(
        DocumentChunk chunk,
        QueryProfile? queryProfile,
        IReadOnlyList<string> signals)
    {
        var focusTerms = queryProfile?.FocusTerms
            .Where(term => !LocalAiService.IsIntentExpansionOnlyToken(term))
            .ToArray() ?? [];
        var sentences = LocalAiService.GetCleanSentences(chunk.Text, 8)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
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
            var cleaned = LocalAiService.CleanStructuredSentence(candidate)
                .Trim('，', '。', '；', '：', ':', '/', '\\', '|', ' ');
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned.Length <= 24 ? cleaned : cleaned[..24].TrimEnd();
            }
        }

        return "组成项";
    }

    private static int CountArchitectureSignals(string sentence)
    {
        var signals = new[] { "核心", "控制", "采集", "执行", "显示", "通信", "调度", "模块", "架构", "层", "接口", "数据", "状态", "参数" };
        return signals.Count(signal => sentence.Contains(signal, StringComparison.Ordinal));
    }

    private static int CountCompositionStructureSignals(string text)
    {
        var signals = new[] { "组成", "构成", "包括", "包含", "模块", "架构", "配置", "条件", "环境", "硬件", "软件", "接口", "依赖", "实验环境", "运行环境", "部署环境" };
        return signals.Count(signal => text.Contains(signal, StringComparison.Ordinal));
    }

    internal static int CountProcedureStructureSignals(string text)
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
        if (LocalAiService.IsParameterHeavySentence(chunk.Text))
        {
            score -= 4;
        }

        return score;
    }
}
