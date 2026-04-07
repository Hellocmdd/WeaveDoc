using System.Diagnostics;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / PDF / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;

    public PandocPipeline(string? pandocPath = null)
    {
        _pandocPath = pandocPath
            ?? Path.Combine(AppContext.BaseDirectory, "tools", "pandoc-3.9.0.2", "pandoc.exe");
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

    /// <summary>Markdown → PDF（XeLaTeX 中文支持）</summary>
    public async Task<string> ToPdfAsync(
        string inputPath, string outputPath,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(inputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables",
            "--pdf-engine", "xelatex",
            "-V", "CJKmainfont=宋体",
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
