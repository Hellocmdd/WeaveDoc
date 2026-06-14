using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeaveDoc.Rag.Services;

internal sealed class LlamaServerChatClient
{
    private readonly HttpClient _httpClient;
    private readonly RagOptions _options;
    private readonly CloudApiSettings _cloudSettings;
    private readonly bool _isCloud;

    public LlamaServerChatClient(HttpClient httpClient, RagOptions options, CloudApiSettings cloudSettings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cloudSettings = cloudSettings ?? throw new ArgumentNullException(nameof(cloudSettings));
        _isCloud = _cloudSettings.ChatProvider == "cloud";
    }

    public async Task EnsureServerAvailableAsync(CancellationToken cancellationToken)
    {
        if (_isCloud)
        {
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, Math.Min(_options.HttpTimeoutSeconds, 30))));

        using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"llama-server 健康检查失败: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken, string? systemPrompt = null)
    {
        var messages = new List<ChatCompletionMessage>
        {
            new("system", string.IsNullOrWhiteSpace(systemPrompt)
                ? "你是一个严格遵守上下文约束的本地文档问答助手。"
                : systemPrompt.Trim()),
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

    /// <summary>
    /// Streams completion token deltas over the OpenAI-compatible
    /// <c>/chat/completions</c> endpoint using Server-Sent Events (<c>stream: true</c>).
    /// Mirrors <see cref="CompleteAsync"/>'s <c>finish_reason=="length"</c> continuation.
    /// </summary>
    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        string? systemPrompt = null)
    {
        var messages = new List<ChatCompletionMessage>
        {
            new("system", string.IsNullOrWhiteSpace(systemPrompt)
                ? "你是一个严格遵守上下文约束的本地文档问答助手。"
                : systemPrompt.Trim()),
            new("user", prompt)
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.HttpTimeoutSeconds));

        var accumulated = new StringBuilder();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = BuildCompletionRequest(messages, stream: true);
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var label = _isCloud ? _cloudSettings.EffectiveProviderLabel : "llama-server";
                throw new TimeoutException(
                    $"调用 {label} 超时（{_options.HttpTimeoutSeconds} 秒）。可通过环境变量 LLAMA_SERVER_TIMEOUT_SECONDS 调大超时时间。");
            }

            string? finishReason = null;
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var label = _isCloud ? _cloudSettings.EffectiveProviderLabel : "llama-server";
                    throw new InvalidOperationException($"{label} 调用失败: {(int)response.StatusCode} {response.ReasonPhrase}, body={body}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }

                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null)
                    {
                        break; // end of stream
                    }

                    if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var payload = line["data:".Length..].TrimStart();
                    if (payload.Length == 0)
                    {
                        continue;
                    }

                    if (payload.Equals("[DONE]", StringComparison.Ordinal))
                    {
                        break;
                    }

                    ChatCompletionStreamResponse? chunk;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<ChatCompletionStreamResponse>(payload);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    var choice = chunk?.Choices?.FirstOrDefault();
                    var delta = choice?.Delta?.Content;
                    if (!string.IsNullOrEmpty(delta))
                    {
                        accumulated.Append(delta);
                        yield return delta;
                    }

                    if (!string.IsNullOrEmpty(choice?.FinishReason))
                    {
                        finishReason = choice.FinishReason;
                    }
                }
            }

            if (!string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            // finish_reason=="length": keep generating, mirroring CompleteAsync's continuation.
            accumulated.AppendLine();
            yield return "\n";
            messages.Add(new ChatCompletionMessage("assistant", accumulated.ToString().Trim()));
            messages.Add(new ChatCompletionMessage("user", "继续，紧接上文输出，不要重复前文，保持同样格式直到回答完整。"));
        }
    }

    private async Task<ChatCompletionResponse?> SendCompletionAsync(
        IReadOnlyList<ChatCompletionMessage> messages,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        using var request = BuildCompletionRequest(messages, stream: false);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, timeoutToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            var label = _isCloud ? _cloudSettings.EffectiveProviderLabel : "llama-server";
            throw new TimeoutException(
                $"调用 {label} 超时（{_options.HttpTimeoutSeconds} 秒）。可通过环境变量 LLAMA_SERVER_TIMEOUT_SECONDS 调大超时时间。");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(callerToken).ConfigureAwait(false);
                var label = _isCloud ? _cloudSettings.EffectiveProviderLabel : "llama-server";
                throw new InvalidOperationException($"{label} 调用失败: {(int)response.StatusCode} {response.ReasonPhrase}, body={body}");
            }

            return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: callerToken).ConfigureAwait(false);
        }
    }

    private HttpRequestMessage BuildCompletionRequest(IReadOnlyList<ChatCompletionMessage> messages, bool stream)
    {
        ThinkingParam? thinking = null;
        string model;

        if (_isCloud)
        {
            model = _cloudSettings.CloudModel;
            if (_cloudSettings.CloudEnableThinking)
            {
                thinking = new ThinkingParam("enabled", _cloudSettings.CloudReasoningEffort);
            }
        }
        else
        {
            model = _options.ChatModel;
        }

        var payload = new ChatCompletionRequest(
            model,
            messages,
            _options.Temperature,
            _options.MaxTokens,
            stream,
            thinking);

        var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/chat/completions"))
        {
            Content = JsonContent.Create(payload)
        };

        if (_isCloud)
        {
            request.Headers.Add("Authorization", $"Bearer {_cloudSettings.CloudApiKey}");
        }

        return request;
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = _isCloud ? _cloudSettings.CloudBaseUrl : _options.LlamaServerBaseUrl;
        var normalizedBase = baseUrl.TrimEnd('/');
        return new Uri($"{normalizedBase}{relativePath}", UriKind.Absolute);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("thinking")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ThinkingParam? Thinking);

    private sealed record ThinkingParam(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("effort")] string Effort);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatCompletionStreamResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionStreamChoice>? Choices);

    private sealed record ChatCompletionStreamChoice(
        [property: JsonPropertyName("delta")] ChatCompletionDelta? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatCompletionDelta(
        [property: JsonPropertyName("content")] string? Content);
}
