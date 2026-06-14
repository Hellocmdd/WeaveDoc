using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WeaveDoc.Converter.Tests;

internal sealed class TestImageServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly byte[] _body;
    private readonly string _contentType;
    private readonly int _statusCode;

    private TestImageServer(byte[] body, string contentType, int statusCode)
    {
        _body = body;
        _contentType = contentType;
        _statusCode = statusCode;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}/image.png";
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public string Url { get; }

    public static TestImageServer StartPng(int statusCode = 200) =>
        new(PngBytes(), "image/png", statusCode);

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        try { await _acceptLoop; } catch { }
        _cts.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        var buffer = new byte[2048];
        try { await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token); } catch { }

        var reason = _statusCode == 200 ? "OK" : "Not Found";
        var body = _statusCode == 200 ? _body : Encoding.UTF8.GetBytes("not found");
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {_statusCode} {reason}\r\nContent-Type: {_contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, _cts.Token);
        await stream.WriteAsync(body, _cts.Token);
    }

    internal static byte[] PngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR42mMAAQAABQABDQottAAAAABJRU5ErkJggg==");
}
