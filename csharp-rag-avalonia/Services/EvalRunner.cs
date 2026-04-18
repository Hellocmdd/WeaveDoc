using System.Globalization;
using System.Text;
using System.Text.Json;
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

            var matched = MatchExpectedKeywords(answer, item.ExpectedKeywords);
            var requiredMatches = GetRequiredMatchCount(item);
            matchedExpectations += matched;
            totalExpectations += item.ExpectedKeywords?.Count ?? 0;
            var passed = item.ExpectedKeywords is null || item.ExpectedKeywords.Count == 0 || matched >= requiredMatches;
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
                answer,
                debug));

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

        var summary = new EvalSummaryReport(
            baseline.Name ?? Path.GetFileNameWithoutExtension(fullPath),
            fullPath,
            total,
            matchedCases,
            totalExpectations,
            matchedExpectations,
            totalExpectations > 0 ? matchedExpectations * 100.0 / totalExpectations : null,
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
        string Answer,
        string RetrievalDebug);

    private sealed record EvalSummaryReport(
        string Name,
        string BaselinePath,
        int TotalCases,
        int MatchedCases,
        int TotalExpectations,
        int MatchedExpectations,
        double? KeywordCoveragePercent,
        DateTimeOffset ExecutedAt,
        IReadOnlyList<EvalCaseReport> Cases);
}
