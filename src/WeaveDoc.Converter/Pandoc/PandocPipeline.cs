using System.Diagnostics;

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

        // 自动发现并附加 Lua Filters（custom-style 注入等）
        foreach (var filter in DiscoverLuaFilters())
        {
            args.AddRange(new[] { "--lua-filter", Quote(filter) });
        }

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
