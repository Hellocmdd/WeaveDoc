using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RagAvalonia.Models;

namespace RagAvalonia.Services;

internal static class EvalRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> RunAsync(string evalFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evalFilePath);

        var fullPath = Path.GetFullPath(evalFilePath);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"[eval] baseline file not found: {fullPath}");
            return 1;
        }

        EvalBaselineFile? baseline;
        await using (var stream = File.OpenRead(fullPath))
        {
            baseline = await JsonSerializer.DeserializeAsync<EvalBaselineFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        if (baseline?.Cases is null || baseline.Cases.Count == 0)
        {
            Console.Error.WriteLine($"[eval] no evaluation cases found in: {fullPath}");
            return 1;
        }

        using var service = new LocalAiService();
        await service.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var total = baseline.Cases.Count;
        var matchedCases = 0;
        var matchedExpectations = 0;
        var totalExpectations = 0;
        var totalRetrievalExpectations = 0;
        var matchedTopChunkExpectations = 0;
        var matchedContextExpectations = 0;
        var citationPrecisionSum = 0d;
        var citationRecallSum = 0d;
        var caseReports = new List<EvalCaseReport>(baseline.Cases.Count);

        Console.WriteLine($"[eval] baseline: {fullPath}");
        Console.WriteLine($"[eval] cases: {total}");
        Console.WriteLine();

        for (var index = 0; index < baseline.Cases.Count; index++)
        {
            var item = baseline.Cases[index];
            if (string.IsNullOrWhiteSpace(item.Question))
            {
                continue;
            }

            var history = new List<ChatTurn>();
            if (item.History?.Count > 0)
            {
                foreach (var turn in item.History)
                {
                    if (string.IsNullOrWhiteSpace(turn.Role) || string.IsNullOrWhiteSpace(turn.Content))
                    {
                        continue;
                    }

                    history.Add(new ChatTurn(turn.Role, turn.Content, turn.Role == "用户"));
                }
            }

            var answer = await service.AskAsync(item.Question, history, cancellationToken).ConfigureAwait(false);
            var debug = service.LastRetrievalDebug;
            var rankedChunks = service.LastRankedChunkSnapshots;
            var contextChunks = service.LastContextChunkSnapshots;

            var matched = MatchExpectedKeywords(answer, item.ExpectedKeywords);
            var requiredMatches = GetRequiredMatchCount(item);
            var structuralPassed = PassesStructuralChecks(answer, item);
            var answerCheck = EvaluateAnswerSignals(answer, item);
            var retrievalCheck = EvaluateRetrievalSignals(rankedChunks, contextChunks, item);
            var citationCheck = EvaluateCitationSignals(answer, item);
            matchedExpectations += matched;
            totalExpectations += item.ExpectedKeywords?.Count ?? 0;
            totalRetrievalExpectations += retrievalCheck.TotalExpectedSignals;
            matchedTopChunkExpectations += retrievalCheck.TopChunkMatchedSignals;
            matchedContextExpectations += retrievalCheck.ContextMatchedSignals;
            citationPrecisionSum += citationCheck.Precision;
            citationRecallSum += citationCheck.Recall;
            var keywordPassed = answerCheck.Passed;
            var retrievalPassed = retrievalCheck.Passed;
            var citationPassed = citationCheck.Passed;
            var passed = keywordPassed && structuralPassed && retrievalPassed && citationPassed;
            if (passed)
            {
                matchedCases++;
            }

            caseReports.Add(new EvalCaseReport(
                item.Id ?? $"case-{index + 1}",
                item.Category ?? "general",
                item.Question,
                item.ExpectedKeywords ?? [],
                matched,
                requiredMatches,
                passed,
                structuralPassed,
                retrievalPassed,
                citationPassed,
                answerCheck.MatchedSignals,
                answerCheck.RequiredSignals,
                retrievalCheck.TopChunkMatchedSignals,
                retrievalCheck.ContextMatchedSignals,
                retrievalCheck.RequiredTopChunkSignals,
                retrievalCheck.RequiredContextSignals,
                answerCheck.ExpectedSignals,
                retrievalCheck.ExpectedSignals,
                citationCheck.MatchedExpectedCitations,
                citationCheck.TotalExpectedCitations,
                citationCheck.AnswerCitationCount,
                citationCheck.Precision,
                citationCheck.Recall,
                answer,
                debug,
                rankedChunks,
                contextChunks));

            Console.WriteLine($"=== Case {index + 1}: {item.Id ?? $"case-{index + 1}"} ===");
            if (!string.IsNullOrWhiteSpace(item.Category))
            {
                Console.WriteLine($"Category: {item.Category}");
            }
            Console.WriteLine($"Question: {item.Question}");
            if (item.ExpectedKeywords?.Count > 0)
            {
                Console.WriteLine($"Expected keywords: {string.Join(", ", item.ExpectedKeywords)}");
                Console.WriteLine($"Matched keywords: {matched}/{item.ExpectedKeywords.Count}");
                if (requiredMatches != item.ExpectedKeywords.Count)
                {
                    Console.WriteLine($"Required matches: {requiredMatches}");
                }
            }
            Console.WriteLine($"Structural checks: {(structuralPassed ? "pass" : "fail")}");
            if (answerCheck.ExpectedSignals.Count > 0)
            {
                Console.WriteLine($"Answer signal checks: {(answerCheck.Passed ? "pass" : "fail")} ({answerCheck.MatchedSignals}/{answerCheck.ExpectedSignals.Count})");
            }
            if (retrievalCheck.TotalExpectedSignals > 0)
            {
                Console.WriteLine($"Retrieval checks: {(retrievalPassed ? "pass" : "fail")} (top {retrievalCheck.TopChunkMatchedSignals}/{retrievalCheck.ExpectedSignals.Count}, context {retrievalCheck.ContextMatchedSignals}/{retrievalCheck.ExpectedSignals.Count})");
            }
            if (item.ExpectedCitations?.Count > 0)
            {
                Console.WriteLine($"Citation checks: {(citationPassed ? "pass" : "fail")} (matched {citationCheck.MatchedExpectedCitations}/{citationCheck.TotalExpectedCitations}, precision {citationCheck.Precision:P0}, recall {citationCheck.Recall:P0})");
            }

            Console.WriteLine("Answer:");
            Console.WriteLine(answer);
            Console.WriteLine();
            Console.WriteLine("Retrieval debug:");
            Console.WriteLine(debug);
            Console.WriteLine();
        }

        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Case pass count: {matchedCases}/{total}");
        if (totalExpectations > 0)
        {
            Console.WriteLine($"Keyword coverage: {matchedExpectations}/{totalExpectations} ({(matchedExpectations * 100.0 / totalExpectations):F1}%)");
        }
        else
        {
            Console.WriteLine("Keyword coverage: N/A");
        }
        if (totalRetrievalExpectations > 0)
        {
            Console.WriteLine($"Top chunk signal coverage: {matchedTopChunkExpectations}/{totalRetrievalExpectations} ({(matchedTopChunkExpectations * 100.0 / totalRetrievalExpectations):F1}%)");
            Console.WriteLine($"Context signal coverage: {matchedContextExpectations}/{totalRetrievalExpectations} ({(matchedContextExpectations * 100.0 / totalRetrievalExpectations):F1}%)");
        }
        else
        {
            Console.WriteLine("Top chunk signal coverage: N/A");
            Console.WriteLine("Context signal coverage: N/A");
        }
        if (caseReports.Count > 0)
        {
            Console.WriteLine($"Average citation precision: {(citationPrecisionSum / caseReports.Count):P1}");
            Console.WriteLine($"Average citation recall: {(citationRecallSum / caseReports.Count):P1}");
        }

        var summary = new EvalSummaryReport(
            baseline.Name ?? Path.GetFileNameWithoutExtension(fullPath),
            fullPath,
            total,
            matchedCases,
            totalExpectations,
            matchedExpectations,
            totalExpectations > 0 ? matchedExpectations * 100.0 / totalExpectations : null,
            totalRetrievalExpectations,
            matchedTopChunkExpectations,
            totalRetrievalExpectations > 0 ? matchedTopChunkExpectations * 100.0 / totalRetrievalExpectations : null,
            matchedContextExpectations,
            totalRetrievalExpectations > 0 ? matchedContextExpectations * 100.0 / totalRetrievalExpectations : null,
            caseReports.Count > 0 ? citationPrecisionSum / caseReports.Count : null,
            caseReports.Count > 0 ? citationRecallSum / caseReports.Count : null,
            DateTimeOffset.Now,
            caseReports);

        var (jsonReportPath, markdownReportPath) = await SaveReportsAsync(summary, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"JSON report: {jsonReportPath}");
        Console.WriteLine($"Markdown report: {markdownReportPath}");

        return matchedCases == total ? 0 : 2;
    }

    private static int MatchExpectedKeywords(string answer, IReadOnlyList<string>? expectedKeywords)
    {
        if (expectedKeywords is null || expectedKeywords.Count == 0)
        {
            return 0;
        }

        var normalizedAnswer = answer.ToLowerInvariant();
        return expectedKeywords.Count(keyword => normalizedAnswer.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static SignalCheckResult EvaluateAnswerSignals(string answer, EvalCase item)
    {
        var expectedSignals = item.AnswerSignals ?? item.ExpectedKeywords ?? [];
        var matchedSignals = MatchSignals(answer, expectedSignals);
        var requiredSignals = GetRequiredSignalCount(expectedSignals, item.MinAnswerSignals, item.MinMatchedKeywords);
        var passed = expectedSignals.Count == 0 || matchedSignals >= requiredSignals;
        return new SignalCheckResult(expectedSignals, matchedSignals, requiredSignals, passed);
    }

    private static RetrievalCheckResult EvaluateRetrievalSignals(
        IReadOnlyList<LocalAiService.RetrievalChunkSnapshot> rankedChunks,
        IReadOnlyList<LocalAiService.RetrievalChunkSnapshot> contextChunks,
        EvalCase item)
    {
        var expectedSignals = item.RetrievalSignals ?? [];
        if (expectedSignals.Count == 0)
        {
            return new RetrievalCheckResult(expectedSignals, 0, 0, 0, 0, true);
        }

        var topChunkText = rankedChunks.FirstOrDefault() is { } topChunk
            ? BuildChunkInspectionText(topChunk)
            : string.Empty;
        var contextText = string.Join("\n\n", contextChunks.Select(BuildChunkInspectionText));
        var topChunkMatchedSignals = MatchSignals(topChunkText, expectedSignals);
        var contextMatchedSignals = MatchSignals(contextText, expectedSignals);
        var requiredTopChunkSignals = GetRequiredSignalCount(expectedSignals, item.MinTopChunkSignals, null);
        var requiredContextSignals = GetRequiredSignalCount(expectedSignals, item.MinContextSignals, null);
        var passed = topChunkMatchedSignals >= requiredTopChunkSignals
            && contextMatchedSignals >= requiredContextSignals;

        return new RetrievalCheckResult(
            expectedSignals,
            topChunkMatchedSignals,
            contextMatchedSignals,
            requiredTopChunkSignals,
            requiredContextSignals,
            passed);
    }

    private static CitationCheckResult EvaluateCitationSignals(string answer, EvalCase item)
    {
        var expectedCitations = item.ExpectedCitations ?? [];
        var answerCitations = CitationRegex.Matches(answer)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (expectedCitations.Count == 0)
        {
            return new CitationCheckResult(0, 0, answerCitations.Length, 1d, 1d, true);
        }

        var matchedExpectedCitations = expectedCitations.Count(expected =>
            answerCitations.Any(actual => actual.Contains(expected, StringComparison.OrdinalIgnoreCase)));
        var precision = answerCitations.Length == 0 ? 0d : matchedExpectedCitations / (double)answerCitations.Length;
        var recall = expectedCitations.Count == 0 ? 1d : matchedExpectedCitations / (double)expectedCitations.Count;
        var requiredCitations = GetRequiredSignalCount(expectedCitations, item.MinMatchedCitations, null);
        var passed = matchedExpectedCitations >= requiredCitations;

        return new CitationCheckResult(
            matchedExpectedCitations,
            expectedCitations.Count,
            answerCitations.Length,
            precision,
            recall,
            passed);
    }

    private static int GetRequiredMatchCount(EvalCase item)
    {
        if (item.ExpectedKeywords is null || item.ExpectedKeywords.Count == 0)
        {
            return 0;
        }

        if (item.MinMatchedKeywords is int explicitMin)
        {
            return Math.Clamp(explicitMin, 0, item.ExpectedKeywords.Count);
        }

        return item.ExpectedKeywords.Count;
    }

    private static int GetRequiredSignalCount(IReadOnlyList<string> expectedSignals, int? explicitMin, int? legacyMin)
    {
        if (expectedSignals.Count == 0)
        {
            return 0;
        }

        if (explicitMin is int minSignals)
        {
            return Math.Clamp(minSignals, 0, expectedSignals.Count);
        }

        if (legacyMin is int legacy)
        {
            return Math.Clamp(legacy, 0, expectedSignals.Count);
        }

        return expectedSignals.Count;
    }

    private static int MatchSignals(string text, IReadOnlyList<string> expectedSignals)
    {
        if (expectedSignals.Count == 0)
        {
            return 0;
        }

        var normalized = NormalizeForMatching(text);
        return expectedSignals.Count(signal => normalized.Contains(NormalizeForMatching(signal), StringComparison.Ordinal));
    }

    private static bool PassesStructuralChecks(string answer, EvalCase item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return true;
        }

        var normalized = NormalizeForMatching(answer);
        return item.Id switch
        {
            "stm32-remote" => ContainsAll(normalized, "usart", "mqtt")
                && ContainsAny(normalized, "app", "手机app")
                && ContainsAny(normalized, "阈值", "指令", "手动触发", "即时灌溉"),
            "stm32-remote-detailed" => ContainsAll(normalized, "usart", "esp-01s", "mqtt")
                && ContainsAny(normalized, "json", "封装")
                && ContainsAny(normalized, "app", "手机app")
                && ContainsAny(normalized, "阈值", "指令", "手动触发", "即时灌溉"),
            "stm32-control-prefixed" => ContainsAll(normalized, "模糊pid", "土壤湿度", "电磁阀")
                && ContainsAny(normalized, "环境温湿度", "环境数据", "传感器")
                && ContainsAny(normalized, "pwm", "占空比", "开度"),
            "stm32-control" => ContainsAll(normalized, "模糊pid", "土壤湿度", "电磁阀")
                && ContainsAny(normalized, "pwm", "占空比", "开度"),
            "follow-up-detail" => ContainsAll(normalized, "模糊pid", "土壤湿度", "电磁阀")
                && ContainsAny(normalized, "pwm", "占空比", "开度")
                && ContainsAny(normalized, "环境温湿度", "补偿", "蒸发"),
            "stm32-no-answer-bluetooth" => ContainsAny(normalized, "我不知道", "当前文档未覆盖"),
            "stm32-summary-json-scoped" => ContainsAny(normalized, "模糊pid", "电磁阀", "交互显示")
                && !ContainsAny(normalized, "englishabstract", "englishkeywords"),
            "stm32-remote-json-scoped" => ContainsAll(normalized, "mqtt")
                && ContainsAny(normalized, "json", "封装")
                && ContainsAny(normalized, "app", "手机app")
                && ContainsAny(normalized, "阈值", "指令", "即时灌溉"),
            "geology-system-architecture" => ContainsAny(normalized, "前后端分离", "restfulapi")
                && ContainsAll(normalized, "springboot")
                && ContainsAny(normalized, "vue3", "vue")
                && ContainsAny(normalized, "mybatis-plus", "mybatisplus")
                && ContainsAny(normalized, "mysql", "echarts"),
            "geology-system-modules" => ContainsAny(normalized, "信息抽取", "抽取模块")
                && ContainsAny(normalized, "知识图谱", "图谱")
                && ContainsAny(normalized, "多模态检索", "多模态")
                && ContainsAny(normalized, "多特征关联", "关联分析", "关联融合"),
            "geology-modules-no-cross-doc" => ContainsAny(normalized, "信息抽取", "抽取模块")
                && ContainsAny(normalized, "知识图谱", "图谱")
                && ContainsAny(normalized, "多模态检索", "多模态")
                && !ContainsAny(normalized, "电磁阀", "模糊pid", "stm32"),
            _ => true
        };
    }

    private static string NormalizeForMatching(string text)
    {
        return Regex.Replace(text.ToLowerInvariant(), "\\s+", string.Empty);
    }

    private static string BuildChunkInspectionText(LocalAiService.RetrievalChunkSnapshot chunk)
    {
        return $"{chunk.Citation}\n{chunk.FilePath}\n{chunk.SectionTitle}\n{chunk.StructurePath}\n{chunk.ContentKind}\n{chunk.Text}";
    }

    private static bool ContainsAll(string text, params string[] tokens)
    {
        return tokens.All(token => text.Contains(token, StringComparison.Ordinal));
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
    }

    private static async Task<(string JsonReportPath, string MarkdownReportPath)> SaveReportsAsync(EvalSummaryReport summary, CancellationToken cancellationToken)
    {
        var reportDirectory = GetReportDirectory();
        Directory.CreateDirectory(reportDirectory);

        var timestamp = summary.ExecutedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var slug = SanitizeFilePart(summary.Name);
        var jsonReportPath = Path.Combine(reportDirectory, $"{timestamp}-{slug}.json");
        var markdownReportPath = Path.Combine(reportDirectory, $"{timestamp}-{slug}.md");

        var reportJsonOptions = new JsonSerializerOptions(JsonOptions)
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(jsonReportPath, JsonSerializer.Serialize(summary, reportJsonOptions), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(markdownReportPath, BuildMarkdownReport(summary), cancellationToken).ConfigureAwait(false);
        return (jsonReportPath, markdownReportPath);
    }

    private static string GetReportDirectory()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("RAG_EVAL_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        return Path.Combine(WorkspacePaths.FindWorkspaceRoot(), ".eval");
    }

    private static string SanitizeFilePart(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '-' : character);
        }

        return string.IsNullOrWhiteSpace(builder.ToString()) ? "eval-report" : builder.ToString().Trim();
    }

    private static string BuildMarkdownReport(EvalSummaryReport summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Eval Report: {summary.Name}");
        builder.AppendLine();
        builder.AppendLine($"- Baseline: `{summary.BaselinePath}`");
        builder.AppendLine($"- Executed at: `{summary.ExecutedAt:yyyy-MM-dd HH:mm:ss zzz}`");
        builder.AppendLine($"- Case pass count: `{summary.MatchedCases}/{summary.TotalCases}`");
        if (summary.KeywordCoveragePercent is double coverage)
        {
            builder.AppendLine($"- Keyword coverage: `{summary.MatchedExpectations}/{summary.TotalExpectations} ({coverage:F1}%)`");
        }
        else
        {
            builder.AppendLine("- Keyword coverage: `N/A`");
        }
        if (summary.TopChunkSignalCoveragePercent is double topCoverage)
        {
            builder.AppendLine($"- Top chunk signal coverage: `{summary.MatchedTopChunkSignals}/{summary.TotalRetrievalSignals} ({topCoverage:F1}%)`");
        }
        else
        {
            builder.AppendLine("- Top chunk signal coverage: `N/A`");
        }
        if (summary.ContextSignalCoveragePercent is double contextCoverage)
        {
            builder.AppendLine($"- Context signal coverage: `{summary.MatchedContextSignals}/{summary.TotalRetrievalSignals} ({contextCoverage:F1}%)`");
        }
        else
        {
            builder.AppendLine("- Context signal coverage: `N/A`");
        }
        if (summary.AverageCitationPrecision is double citationPrecision)
        {
            builder.AppendLine($"- Average citation precision: `{citationPrecision:P1}`");
        }
        if (summary.AverageCitationRecall is double citationRecall)
        {
            builder.AppendLine($"- Average citation recall: `{citationRecall:P1}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Cases");
        builder.AppendLine();

        foreach (var item in summary.Cases)
        {
            builder.AppendLine($"### {item.Id}");
            builder.AppendLine();
            builder.AppendLine($"- Category: `{item.Category}`");
            builder.AppendLine($"- Passed: `{(item.Passed ? "yes" : "no")}`");
            builder.AppendLine($"- Question: {item.Question}");
            if (item.ExpectedKeywords.Count > 0)
            {
                builder.AppendLine($"- Expected keywords: `{string.Join("`, `", item.ExpectedKeywords)}`");
                builder.AppendLine($"- Matched keywords: `{item.MatchedKeywords}/{item.ExpectedKeywords.Count}`");
                builder.AppendLine($"- Required matches: `{item.RequiredMatches}`");
            }
            builder.AppendLine($"- Structural checks: `{(item.StructuralChecksPassed ? "pass" : "fail")}`");
            builder.AppendLine($"- Retrieval checks: `{(item.RetrievalChecksPassed ? "pass" : "fail")}`");
            builder.AppendLine($"- Citation checks: `{(item.CitationChecksPassed ? "pass" : "fail")}`");
            if (item.ExpectedAnswerSignals.Count > 0)
            {
                builder.AppendLine($"- Answer signals: `{item.MatchedAnswerSignals}/{item.ExpectedAnswerSignals.Count}`");
                builder.AppendLine($"- Required answer signals: `{item.RequiredAnswerSignals}`");
            }
            if (item.ExpectedRetrievalSignals.Count > 0)
            {
                builder.AppendLine($"- Top chunk signals: `{item.TopChunkMatchedSignals}/{item.ExpectedRetrievalSignals.Count}`");
                builder.AppendLine($"- Context signals: `{item.ContextMatchedSignals}/{item.ExpectedRetrievalSignals.Count}`");
                builder.AppendLine($"- Required top chunk signals: `{item.RequiredTopChunkSignals}`");
                builder.AppendLine($"- Required context signals: `{item.RequiredContextSignals}`");
            }
            if (item.TotalExpectedCitations > 0)
            {
                builder.AppendLine($"- Citation precision: `{item.CitationPrecision:P1}`");
                builder.AppendLine($"- Citation recall: `{item.CitationRecall:P1}`");
                builder.AppendLine($"- Matched expected citations: `{item.MatchedExpectedCitations}/{item.TotalExpectedCitations}`");
                builder.AppendLine($"- Answer citation count: `{item.AnswerCitationCount}`");
            }

            builder.AppendLine();
            builder.AppendLine("**Answer**");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(item.Answer);
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("**Retrieval Debug**");
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(item.RetrievalDebug);
            builder.AppendLine("```");
            if (item.TopRankedChunks.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("**Top Ranked Chunks**");
                builder.AppendLine();
                builder.AppendLine("```text");
                foreach (var chunk in item.TopRankedChunks)
                {
                    builder.AppendLine(chunk.Citation);
                    builder.AppendLine($"{chunk.FilePath} | {chunk.StructurePath} | {chunk.ContentKind}");
                    builder.AppendLine(chunk.Text);
                    builder.AppendLine();
                }
                builder.AppendLine("```");
            }
            if (item.ContextChunks.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("**Context Chunks**");
                builder.AppendLine();
                builder.AppendLine("```text");
                foreach (var chunk in item.ContextChunks)
                {
                    builder.AppendLine(chunk.Citation);
                    builder.AppendLine($"{chunk.FilePath} | {chunk.StructurePath} | {chunk.ContentKind}");
                    builder.AppendLine(chunk.Text);
                    builder.AppendLine();
                }
                builder.AppendLine("```");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private sealed record EvalBaselineFile(string? Name, List<EvalCase> Cases);

    private sealed record EvalCase(
        string? Id,
        string? Category,
        string Question,
        List<string>? ExpectedKeywords,
        int? MinMatchedKeywords,
        List<string>? AnswerSignals,
        int? MinAnswerSignals,
        List<string>? RetrievalSignals,
        int? MinTopChunkSignals,
        int? MinContextSignals,
        List<string>? ExpectedCitations,
        int? MinMatchedCitations,
        List<EvalTurn>? History);

    private sealed record EvalTurn(string Role, string Content);

    private sealed record EvalCaseReport(
        string Id,
        string Category,
        string Question,
        IReadOnlyList<string> ExpectedKeywords,
        int MatchedKeywords,
        int RequiredMatches,
        bool Passed,
        bool StructuralChecksPassed,
        bool RetrievalChecksPassed,
        bool CitationChecksPassed,
        int MatchedAnswerSignals,
        int RequiredAnswerSignals,
        int TopChunkMatchedSignals,
        int ContextMatchedSignals,
        int RequiredTopChunkSignals,
        int RequiredContextSignals,
        IReadOnlyList<string> ExpectedAnswerSignals,
        IReadOnlyList<string> ExpectedRetrievalSignals,
        int MatchedExpectedCitations,
        int TotalExpectedCitations,
        int AnswerCitationCount,
        double CitationPrecision,
        double CitationRecall,
        string Answer,
        string RetrievalDebug,
        IReadOnlyList<LocalAiService.RetrievalChunkSnapshot> TopRankedChunks,
        IReadOnlyList<LocalAiService.RetrievalChunkSnapshot> ContextChunks);

    private sealed record EvalSummaryReport(
        string Name,
        string BaselinePath,
        int TotalCases,
        int MatchedCases,
        int TotalExpectations,
        int MatchedExpectations,
        double? KeywordCoveragePercent,
        int TotalRetrievalSignals,
        int MatchedTopChunkSignals,
        double? TopChunkSignalCoveragePercent,
        int MatchedContextSignals,
        double? ContextSignalCoveragePercent,
        double? AverageCitationPrecision,
        double? AverageCitationRecall,
        DateTimeOffset ExecutedAt,
        IReadOnlyList<EvalCaseReport> Cases);

    private sealed record SignalCheckResult(
        IReadOnlyList<string> ExpectedSignals,
        int MatchedSignals,
        int RequiredSignals,
        bool Passed);

    private sealed record RetrievalCheckResult(
        IReadOnlyList<string> ExpectedSignals,
        int TopChunkMatchedSignals,
        int ContextMatchedSignals,
        int RequiredTopChunkSignals,
        int RequiredContextSignals,
        bool Passed)
    {
        public int TotalExpectedSignals => ExpectedSignals.Count;
    }

    private sealed record CitationCheckResult(
        int MatchedExpectedCitations,
        int TotalExpectedCitations,
        int AnswerCitationCount,
        double Precision,
        double Recall,
        bool Passed);

    private static readonly Regex CitationRegex = new("\\[(?:\\d+|[^\\[\\]\\r\\n|]{1,160}\\s\\|\\s[^\\[\\]\\r\\n|]{1,160}\\s\\|\\sc\\d+)\\]", RegexOptions.Compiled);
}
