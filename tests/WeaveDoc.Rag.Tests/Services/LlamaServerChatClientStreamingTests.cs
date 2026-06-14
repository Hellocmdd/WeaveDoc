using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.Rag.Tests.Services;

public sealed class LlamaServerChatClientStreamingTests
{
    [Fact]
    public async Task StreamCompletionAsync_YieldsContentDeltasAndStopsAtDone()
    {
        var sse =
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";

        var client = NewClient(new StubHandler(sse), cloud: false);

        var deltas = new List<string>();
        await foreach (var delta in client.StreamCompletionAsync("hi", CancellationToken.None))
        {
            deltas.Add(delta);
        }

        Assert.Equal(new[] { "Hel", "lo" }, deltas);
    }

    [Fact]
    public async Task StreamCompletionAsync_LengthFinishReason_ContinuesAndJoinsAttempts()
    {
        var first =
            "data: {\"choices\":[{\"delta\":{\"content\":\"A\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\n" +
            "data: [DONE]\n\n";
        var second =
            "data: {\"choices\":[{\"delta\":{\"content\":\"B\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: [DONE]\n\n";

        var client = NewClient(new StubHandler(first, second), cloud: false);

        var deltas = new List<string>();
        await foreach (var delta in client.StreamCompletionAsync("hi", CancellationToken.None))
        {
            deltas.Add(delta);
        }

        // attempt 1 → "A", length-continuation emits a newline, attempt 2 → "B"
        Assert.Equal(new[] { "A", "\n", "B" }, deltas);
    }

    [Fact]
    public async Task StreamCompletionAsync_CloudProvider_AttachesAuthorizationHeader()
    {
        var sse = "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n";
        var handler = new StubHandler(sse);
        var client = NewClient(handler, cloud: true, apiKey: "secret-key");

        var deltas = new List<string>();
        await foreach (var delta in client.StreamCompletionAsync("hi", CancellationToken.None))
        {
            deltas.Add(delta);
        }

        Assert.Equal(new[] { "ok" }, deltas);
        var auth = Assert.Single(handler.Requests);
        Assert.Equal("Bearer secret-key", auth.Headers.Authorization?.ToString());
    }

    private static LlamaServerChatClient NewClient(StubHandler handler, bool cloud, string apiKey = "")
    {
        var options = RagOptions.LoadFromEnvironment() with
        {
            LlamaServerBaseUrl = "http://localhost/",
            HttpTimeoutSeconds = 5,
        };
        var settings = new CloudApiSettings();
        if (cloud)
        {
            settings.ChatProvider = "cloud";
            settings.CloudApiKey = apiKey;
        }

        var http = new HttpClient(handler);
        return new LlamaServerChatClient(http, options, settings);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string[] _bodies;
        private int _call;
        public List<HttpRequestMessage> Requests { get; } = [];

        public StubHandler(params string[] bodies)
        {
            _bodies = bodies;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var body = _bodies[Math.Min(_call++, _bodies.Length - 1)];
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
