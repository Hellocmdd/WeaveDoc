using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace RagAvalonia.Services;

internal sealed class LlamaServerChatClient
{
    private readonly HttpClient _httpClient;
    private readonly RagOptions _options;

    public LlamaServerChatClient(HttpClient httpClient, RagOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task EnsureServerAvailableAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, Math.Min(_options.HttpTimeoutSeconds, 30))));

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"llama-server 健康检查失败: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        var messages = new List<ChatCompletionMessage>
        {
            new("system", "你是一个严格遵守上下文约束的本地文档问答助手。"),
            new("user", prompt)
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.HttpTimeoutSeconds));

        var answer = new StringBuilder();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await SendCompletionAsync(messages, timeoutCts.Token, cancellationToken).ConfigureAwait(false);
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                if (answer.Length > 0 && !char.IsWhiteSpace(answer[^1]))
                {
                    answer.AppendLine();
                }

                answer.Append(content);
            }

            var finishReason = result?.Choices?.FirstOrDefault()?.FinishReason;
            if (!string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            messages.Add(new ChatCompletionMessage("assistant", answer.ToString().Trim()));
            messages.Add(new ChatCompletionMessage("user", "继续，紧接上文输出，不要重复前文，保持同样格式直到回答完整。"));
        }

        return answer.ToString().Trim();
    }

    private async Task<ChatCompletionResponse?> SendCompletionAsync(
        IReadOnlyList<ChatCompletionMessage> messages,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        var payload = new ChatCompletionRequest(
            _options.ChatModel,
            messages,
            _options.Temperature,
            _options.MaxTokens,
            false);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                BuildUri("/v1/chat/completions"),
                payload,
                timeoutToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"调用 llama-server 超时（{_options.HttpTimeoutSeconds} 秒）。可通过环境变量 LLAMA_SERVER_TIMEOUT_SECONDS 调大超时时间。");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(callerToken).ConfigureAwait(false);
                throw new InvalidOperationException($"llama-server 调用失败: {(int)response.StatusCode} {response.ReasonPhrase}, body={body}");
            }

            return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: callerToken).ConfigureAwait(false);
        }
    }

    private Uri BuildUri(string relativePath)
    {
        var normalizedBase = _options.LlamaServerBaseUrl.TrimEnd('/');
        return new Uri($"{normalizedBase}{relativePath}", UriKind.Absolute);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);
}
