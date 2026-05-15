using System.Diagnostics;
using System.Text;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;

    /// <param name="pandocPath">Pandoc 可执行文件路径，默认依次查找 tools/pandoc/pandoc.exe、系统 PATH</param>
    public PandocPipeline(string? pandocPath = null)
    {
        _pandocPath = pandocPath
            ?? ResolvePandocPath();
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
        var normalizedInputPath = await PrepareMarkdownInputAsync(inputPath, ct);
        var args = new List<string>
        {
            Quote(normalizedInputPath),
            "-f", "markdown+tex_math_dollars+pipe_tables+raw_html-subscript",
            "-t", "docx",
            "-o", Quote(outputPath),
            "--standalone"
        };

        if (referenceDoc != null)
            args.AddRange(new[] { "--reference-doc", Quote(referenceDoc) });
        if (luaFilter != null)
            args.AddRange(new[] { "--lua-filter", Quote(luaFilter) });

        // 自动发现并附加 Lua Filters（custom-style 注入等）
        foreach (var filter in DiscoverLuaFilters())
        {
            args.AddRange(new[] { "--lua-filter", Quote(filter) });
        }

        try
        {
            return await RunAsync(args, ct);
        }
        finally
        {
            DeleteTempInput(normalizedInputPath, inputPath);
        }
    }

    /// <summary>导出 AST JSON</summary>
    public async Task<string> ToAstJsonAsync(
        string inputPath, CancellationToken ct = default)
    {
        var normalizedInputPath = await PrepareMarkdownInputAsync(inputPath, ct);
        var args = new List<string> { Quote(normalizedInputPath), "-t", "json" };
        try
        {
            return await RunAsync(args, ct);
        }
        finally
        {
            DeleteTempInput(normalizedInputPath, inputPath);
        }
    }

    private static async Task<string> PrepareMarkdownInputAsync(string inputPath, CancellationToken ct)
    {
        if (!File.Exists(inputPath))
            return inputPath;

        var markdown = await File.ReadAllTextAsync(inputPath, ct);
        var normalized = MarkdownHtmlTableNormalizer.NormalizeHtmlTables(markdown);
        normalized = MarkdownHtmlImageNormalizer.NormalizeHtmlImages(normalized);
        normalized = MarkdownMathNormalizer.NormalizeDollarMath(normalized);
        if (normalized == markdown)
            return inputPath;

        var tempPath = Path.Combine(Path.GetTempPath(), $"weavedoc-md-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempPath, normalized, Encoding.UTF8, ct);
        return tempPath;
    }

    private static void DeleteTempInput(string normalizedInputPath, string originalInputPath)
    {
        if (string.Equals(normalizedInputPath, originalInputPath, StringComparison.OrdinalIgnoreCase))
            return;

        try { File.Delete(normalizedInputPath); } catch { }
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
        {
            throw new PandocException(
                $"Pandoc 转换失败，退出码 {process.ExitCode}",
                process.ExitCode,
                _pandocPath,
                psi.Arguments,
                stdout,
                stderr);
        }

        return stdout;
    }

    /// <summary>
    /// 自动发现 LuaFilters 目录下的所有 .lua 文件
    /// </summary>
    private static List<string> DiscoverLuaFilters()
    {
        var filters = new List<string>();

        // 1. 构建输出目录下的 LuaFilters
        var filterDir = Path.Combine(AppContext.BaseDirectory, "LuaFilters");
        if (!Directory.Exists(filterDir))
        {
            // 2. 从 BaseDirectory 向上查找（开发时定位源码目录）
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "src", "WeaveDoc.Converter", "Pandoc", "LuaFilters");
                if (Directory.Exists(candidate))
                {
                    filterDir = candidate;
                    break;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        if (Directory.Exists(filterDir))
        {
            foreach (var luaFile in Directory.GetFiles(filterDir, "*.lua"))
            {
                filters.Add(luaFile);
            }
        }

        return filters;
    }

    private static string Quote(string path) =>
        path.Contains(' ') ? $"\"{path}\"" : path;
}

public class PandocException : Exception
{
    public PandocException(
        string message,
        int exitCode,
        string pandocPath,
        string arguments,
        string stdout,
        string stderr) : base(message)
    {
        ExitCode = exitCode;
        PandocPath = pandocPath;
        Arguments = arguments;
        Stdout = stdout;
        Stderr = stderr;
    }

    public int ExitCode { get; }
    public string PandocPath { get; }
    public string Arguments { get; }
    public string Stdout { get; }
    public string Stderr { get; }

    public override string ToString()
    {
        var builder = new StringBuilder(base.ToString());
        builder.AppendLine();
        builder.AppendLine($"Pandoc: {PandocPath}");
        builder.AppendLine($"Arguments: {Arguments}");
        if (!string.IsNullOrWhiteSpace(Stderr))
            builder.AppendLine($"stderr: {Stderr.Trim()}");
        if (!string.IsNullOrWhiteSpace(Stdout))
            builder.AppendLine($"stdout: {Stdout.Trim()}");
        return builder.ToString();
    }
}
