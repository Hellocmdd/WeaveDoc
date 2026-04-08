using System.Diagnostics;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / PDF / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;
    private readonly string _tectonicDir;

    /// <param name="pandocPath">Pandoc 可执行文件路径，默认 tools/pandoc/pandoc.exe</param>
    /// <param name="tectonicDir">Tectonic 所在目录，默认 tools/tectonic/</param>
    public PandocPipeline(string? pandocPath = null, string? tectonicDir = null)
    {
        _pandocPath = pandocPath
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "pandoc", "pandoc.exe");
        _tectonicDir = tectonicDir
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "tectonic");
    }

    /// <summary>Markdown → DOCX</summary>
    public async Task<string> ToDocxAsync(
        string inputPath, string outputPath,
        string? referenceDoc = null, string? luaFilter = null,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(inputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables+raw_html",
            "-t", "docx",
            "-o", Quote(outputPath),
            "--standalone"
        };

        if (referenceDoc != null)
            args.AddRange(new[] { "--reference-doc", Quote(referenceDoc) });
        if (luaFilter != null)
            args.AddRange(new[] { "--lua-filter", Quote(luaFilter) });

        return await RunAsync(args, ct);
    }

    /// <summary>Markdown → PDF（Tectonic 引擎，基于 XeTeX 自动管理 TeX 依赖）</summary>
    public async Task<string> ToPdfAsync(
        string inputPath, string outputPath,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(inputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables",
            "--pdf-engine", "tectonic",
            "-V", "mainfont=SimSun",
            "-V", "CJKmainfont=SimSun",
            "-V", "monofont=SimSun",
            "-o", Quote(outputPath)
        };

        return await RunAsync(args, ct);
    }

    /// <summary>导出 AST JSON</summary>
    public async Task<string> ToAstJsonAsync(
        string inputPath, CancellationToken ct = default)
    {
        var args = new List<string> { Quote(inputPath), "-t", "json" };
        return await RunAsync(args, ct);
    }

    private async Task<string> RunAsync(List<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pandocPath,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // 将 Tectonic 目录注入子进程 PATH，使 Pandoc 能找到 tectonic 可执行文件
        if (Directory.Exists(_tectonicDir))
        {
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.EnvironmentVariables["PATH"] = $"{_tectonicDir}{Path.PathSeparator}{existingPath}";
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"无法启动 Pandoc: {_pandocPath}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new Exception($"Pandoc 退出码 {process.ExitCode}: {stderr}");

        return stdout;
    }

    private static string Quote(string path) =>
        path.Contains(' ') ? $"\"{path}\"" : path;
}
