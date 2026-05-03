using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RagAvalonia.Models;

namespace RagAvalonia.Services;

public sealed partial class LocalAiService
{
    private int GetRerankCandidatePoolSize(QueryProfile queryProfile)
    {
        var multiplier = queryProfile.Intent switch
        {
            "procedure" => 3,
            "module_implementation" => 3,
            "module_list" => 3,
            "metadata" => 2,
            _ => 1
        };

        return Math.Clamp(_options.CandidatePoolSize * multiplier, _options.CandidatePoolSize, _options.SparseCandidatePoolSize);
    }

    private async Task<LearnedRerankResult> TryRerankWithLearnedModelAsync(
        string question,
        IReadOnlyList<ScoredChunk> candidates,
        QueryProfile queryProfile,
        int count,
        CancellationToken cancellationToken)
    {
        if (!_options.RerankerEnabled)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "disabled");
        }

        if (candidates.Count <= 1)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "not enough candidates");
        }

        var endpoint = BuildRerankerEndpoint(_options.RerankerBaseUrl);
        var documents = candidates
            .Select(candidate => BuildRerankerDocumentText(candidate.Chunk))
            .ToArray();
        
        var rerankerQuery = BuildRerankerQuery(question, queryProfile);
        var payload = new RerankRequest(_options.RerankerModel, rerankerQuery, documents, Math.Min(count, candidates.Count));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.RerankerTimeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, $"http {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            var scores = ParseRerankScores(document.RootElement);
            if (scores.Count == 0)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "empty response");
            }

            var scoreByIndex = scores
                .Where(score => score.Index >= 0 && score.Index < candidates.Count)
                .GroupBy(score => score.Index)
                .ToDictionary(group => group.Key, group => group.Max(item => item.Score));

            if (scoreByIndex.Count == 0)
            {
                return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "no usable scores");
            }

            var ranked = candidates
                .Select((candidate, index) =>
                {
                    var relevance = scoreByIndex.TryGetValue(index, out var score) ? score : float.NegativeInfinity;
                    
                    return new
                    {
                        Candidate = candidate with { Score = relevance },
                        Relevance = relevance,
                        OriginalRank = index
                    };
                })
                .OrderByDescending(item => item.Relevance)
                .ThenByDescending(item => item.Candidate.Score)
                .ThenBy(item => item.OriginalRank)
                .Take(count)
                .Select(item => item.Candidate)
                .ToArray();

            return new LearnedRerankResult(ranked, true, $"ok ({scoreByIndex.Count}/{candidates.Count})");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, "timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            return new LearnedRerankResult(candidates.Take(count).ToArray(), false, exception.GetType().Name);
        }
    }





    private static string BuildRerankerQuery(string question, QueryProfile queryProfile)
    {
        var suffix = queryProfile.Intent switch
        {
            // Prefer narrow, focused technical sub-sections with specific parameters/values
            // over broad Markdown blocks that aggregate many topics.
            "procedure" or "implementation" => " (关注：精确技术参数、单一功能点的直接描述、具体数值/公式/代码逻辑)",
            // Module implementation: look for the specific operational details of that module
            "module_implementation" => " (关注：该模块的技术参数、工作原理、接口与控制策略的直接描述)",
            "composition" or "module_list" or "list" => " (关注：关键模块名称、硬件组成、技术栈、并列项的直接列举)",
            _ => ""
        };
        return question + suffix;
    }

    private static Uri BuildRerankerEndpoint(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return new Uri($"{normalized}/v1/rerank", UriKind.Absolute);
    }

    private static string BuildRerankerDocumentText(DocumentChunk chunk)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"file: {chunk.FilePath}");
        if (!string.IsNullOrWhiteSpace(chunk.StructurePath))
        {
            builder.AppendLine($"section: {chunk.StructurePath}");
        }
        else if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            builder.AppendLine($"section: {chunk.SectionTitle}");
        }

        builder.AppendLine($"kind: {chunk.ContentKind}");
        builder.AppendLine(BuildChunkRetrievalText(chunk));
        return builder.ToString();
    }

    private static List<RerankScore> ParseRerankScores(JsonElement root)
    {
        var scores = new List<RerankScore>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(root, scores);
            return scores;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return scores;
        }

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(results, scores);
        }
        else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            ParseRerankScoreArray(data, scores);
        }

        return scores;
    }

    private static void ParseRerankScoreArray(JsonElement items, List<RerankScore> scores)
    {
        var fallbackIndex = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                fallbackIndex++;
                continue;
            }

            var index = TryGetIntProperty(item, "index")
                ?? TryGetIntProperty(item, "document_index")
                ?? fallbackIndex;
            var score = TryGetFloatProperty(item, "relevance_score")
                ?? TryGetFloatProperty(item, "score")
                ?? TryGetFloatProperty(item, "relevance");

            if (score.HasValue)
            {
                scores.Add(new RerankScore(index, score.Value));
            }

            fallbackIndex++;
        }
    }


    private static float ComputeCoverageScore(IReadOnlyList<string> focusTerms, DocumentChunk chunk)
    {
        if (focusTerms.Count == 0)
        {
            return 0f;
        }

        var normalized = BuildChunkRetrievalText(chunk).ToLowerInvariant();
        var matched = focusTerms.Count(term => normalized.Contains(term, StringComparison.Ordinal));
        return matched <= 0 ? 0f : matched / (float)focusTerms.Count;
    }
}
