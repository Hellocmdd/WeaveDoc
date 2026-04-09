using System.Diagnostics;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / PDF / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;
    private readonly string _tectonicDir;

    /// <param name="pandocPath">Pandoc 可执行文件路径，默认依次查找 tools/pandoc/pandoc.exe、系统 PATH</param>
    /// <param name="tectonicDir">Tectonic 所在目录，默认 tools/tectonic/</param>
    public PandocPipeline(string? pandocPath = null, string? tectonicDir = null)
    {
        _pandocPath = pandocPath
            ?? ResolvePandocPath();
        _tectonicDir = tectonicDir ?? ResolveToolsDir("tectonic");
    }

    private static string ResolveToolsDir(string toolName)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "tools", toolName);
        if (Directory.Exists(localPath))
            return localPath;

        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tools", toolName);
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "tools", toolName);
    }

    private static string ResolvePandocPath()
    {
        // 1. 构建输出目录下的 tools/pandoc
        var localPath = Path.Combine(AppContext.BaseDirectory, "tools", "pandoc", "pandoc.exe");
        if (File.Exists(localPath))
            return localPath;

        // 2. 从 BaseDirectory 向上查找 tools/pandoc（开发时定位仓库根目录）
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "tools", "pandoc", "pandoc.exe");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // 3. 系统 PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(p, "pandoc.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return localPath;
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

    /// <summary>DOCX → PDF（基于已修正的 DOCX，保留 AFD 样式）</summary>
    public async Task<string> FromDocxToPdfAsync(
        string docxInputPath, string outputPath,
        string? mainFont = null, string? cjkMainFont = null, string? monoFont = null,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            Quote(docxInputPath),
            "-f", "docx",
            "--pdf-engine", "tectonic",
            "-o", Quote(outputPath)
        };

        if (mainFont != null)
            args.AddRange(new[] { "-V", $"mainfont={mainFont}" });
        if (cjkMainFont != null)
            args.AddRange(new[] { "-V", $"CJKmainfont={cjkMainFont}" });
        if (monoFont != null)
            args.AddRange(new[] { "-V", $"monofont={monoFont}" });

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
