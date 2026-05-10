using System.Diagnostics;

namespace RagAvalonia.Services;

public sealed class LlamaServerProcess : IDisposable
{
    private Process? _process;
    private bool _startedByUs;
    private readonly string _label;

    public LlamaServerProcess(string label)
    {
        _label = label;
    }

    public async Task StartIfNeededAsync(
        string modelPath,
        int port,
        string extraArgs,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"http://127.0.0.1:{port}";
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            using var response = await httpClient.GetAsync($"{baseUrl}/health", cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return;
        }
        catch
        {
            // 服务器未运行，正常继续启动
        }

        var binaryPath = ResolveBinaryPath();
        var logPath = Path.Combine(WorkspacePaths.FindWorkspaceRoot(), ".rag", $"llama-{_label}.log");
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(logDir) && !Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        var args = $"-m \"{modelPath}\" --host 127.0.0.1 --port {port} {extraArgs}";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        _process.Start();
        _startedByUs = true;

        // 将日志写入文件（异步，不阻塞）
        _ = WriteLogsAsync(_process, logPath);

        // 轮询等待服务就绪
        await WaitForServerAsync(baseUrl, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_process is null || !_startedByUs)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch
        {
            // 进程已退出
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private static string ResolveBinaryPath()
    {
        var envPath = Environment.GetEnvironmentVariable("LLAMA_SERVER_BINARY");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        var root = WorkspacePaths.FindWorkspaceRoot();
        var localPath = Path.Combine(root, "llama.cpp", "build", "bin", "llama-server");
        if (File.Exists(localPath))
            return localPath;

        // 回退到 PATH
        return "llama-server";
    }

    private async Task WaitForServerAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 30; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await httpClient.GetAsync($"{baseUrl}/health", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // 尚未就绪
            }

            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"{_label} server did not become ready within 30 seconds");
    }

    private static async Task WriteLogsAsync(Process process, string logPath)
    {
        try
        {
            await using var writer = new StreamWriter(logPath, false);
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(writer.BaseStream);
            await stdoutTask.ConfigureAwait(false);
        }
        catch
        {
            // 日志写入失败不影响主流程
        }
    }
}
