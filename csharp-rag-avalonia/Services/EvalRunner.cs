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
            matchedExpectations += matched;
            totalExpectations += item.ExpectedKeywords?.Count ?? 0;
            if (item.ExpectedKeywords is null || item.ExpectedKeywords.Count == 0 || matched == item.ExpectedKeywords.Count)
            {
                matchedCases++;
            }

            Console.WriteLine($"=== Case {index + 1}: {item.Id ?? $"case-{index + 1}"} ===");
            Console.WriteLine($"Question: {item.Question}");
            if (item.ExpectedKeywords?.Count > 0)
            {
                Console.WriteLine($"Expected keywords: {string.Join(", ", item.ExpectedKeywords)}");
                Console.WriteLine($"Matched keywords: {matched}/{item.ExpectedKeywords.Count}");
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

        return 0;
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

    private sealed record EvalBaselineFile(string? Name, List<EvalCase> Cases);

    private sealed record EvalCase(
        string? Id,
        string Question,
        List<string>? ExpectedKeywords,
        List<EvalTurn>? History);

    private sealed record EvalTurn(string Role, string Content);
}
