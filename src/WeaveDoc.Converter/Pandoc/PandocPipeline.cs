using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// Pandoc CLI 封装：Markdown → DOCX / AST JSON
/// </summary>
public class PandocPipeline
{
    private readonly string _pandocPath;
    private readonly bool _useInternalFallback;
    private static readonly string[] CandidateExecutableNames = OperatingSystem.IsWindows()
        ? ["pandoc.exe"]
        : ["pandoc", "pandoc.exe"];
    private static readonly string[] CandidateRelativePaths = OperatingSystem.IsWindows()
        ? ["tools/pandoc/pandoc.exe"]
        : ["tools/pandoc/pandoc", "tools/pandoc/bin/pandoc", "tools/pandoc/pandoc.exe"];

    /// <param name="pandocPath">Pandoc 可执行文件路径，默认依次查找 tools/pandoc/{pandoc,pandoc.exe}、系统 PATH</param>
    public PandocPipeline(string? pandocPath = null)
    {
        _pandocPath = pandocPath
            ?? ResolvePandocPath();
        _useInternalFallback = string.IsNullOrWhiteSpace(_pandocPath) || !File.Exists(_pandocPath);
    }

    private static string ResolvePandocPath()
    {
        // 1. 构建输出目录下的 tools/pandoc
        foreach (var relativePath in CandidateRelativePaths)
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localPath))
                return localPath;
        }

        // 2. 从 BaseDirectory 向上查找 tools/pandoc（开发时定位仓库根目录）
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            foreach (var relativePath in CandidateRelativePaths)
            {
                var candidate = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                    return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        // 3. 系统 PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var p in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var fileName in CandidateExecutableNames)
            {
                var candidate = Path.Combine(p, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "tools", "pandoc", "pandoc.exe")
            : Path.Combine(AppContext.BaseDirectory, "tools", "pandoc", "bin", "pandoc");
    }

    /// <summary>Markdown → DOCX</summary>
    public async Task<string> ToDocxAsync(
        string inputPath, string outputPath,
        string? referenceDoc = null, string? luaFilter = null,
        CancellationToken ct = default)
    {
        if (_useInternalFallback)
        {
            await GenerateDocxFallbackAsync(inputPath, outputPath, ct).ConfigureAwait(false);
            return string.Empty;
        }

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
        if (_useInternalFallback)
        {
            return await GenerateAstJsonFallbackAsync(inputPath, ct).ConfigureAwait(false);
        }

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
        {
            if (_useInternalFallback)
            {
                return string.Empty;
            }

            throw new Exception($"Pandoc 退出码 {process.ExitCode}: {stderr}");
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

    private static async Task GenerateDocxFallbackAsync(string inputPath, string outputPath, CancellationToken ct)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input markdown file not found.", inputPath);
        }

        var markdown = await File.ReadAllTextAsync(inputPath, ct).ConfigureAwait(false);
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);

        foreach (var block in ParseMarkdownBlocks(markdown))
        {
            var paragraph = new Paragraph();
            if (block.StyleId is not null)
            {
                paragraph.AppendChild(new ParagraphProperties(new ParagraphStyleId { Val = block.StyleId }));
            }

            if (!string.IsNullOrWhiteSpace(block.Text))
            {
                var run = paragraph.AppendChild(new Run());
                run.AppendChild(new Text(block.Text) { Space = SpaceProcessingModeValues.Preserve });
            }

            body.AppendChild(paragraph);
        }

        mainPart.Document.Save();
    }

    private static async Task<string> GenerateAstJsonFallbackAsync(string inputPath, CancellationToken ct)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input markdown file not found.", inputPath);
        }

        var markdown = await File.ReadAllTextAsync(inputPath, ct).ConfigureAwait(false);
        var blocks = ParseMarkdownBlocks(markdown)
            .Select(block => new Dictionary<string, object?>
            {
                ["t"] = block.StyleId switch
                {
                    "Heading1" => "Header",
                    "Blockquote" => "BlockQuote",
                    "CodeBlock" => "CodeBlock",
                    _ => "Para"
                },
                ["style"] = block.StyleId,
                ["text"] = block.Text
            })
            .ToArray();

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["blocks"] = blocks
        });
    }

    private static IEnumerable<(string? StyleId, string Text)> ParseMarkdownBlocks(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var buffer = new StringBuilder();
        string? currentStyle = null;
        var inCodeBlock = false;

        var yieldReturn = new List<(string? StyleId, string Text)>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    yieldReturn.Add(("CodeBlock", buffer.ToString().TrimEnd()));
                    buffer.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    if (buffer.Length > 0)
                    {
                        var text = buffer.ToString().TrimEnd();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yieldReturn.Add((currentStyle, text));
                        }
                        buffer.Clear();
                        currentStyle = null;
                    }
                    inCodeBlock = true;
                }

                continue;
            }

            if (inCodeBlock)
            {
                if (buffer.Length > 0)
                {
                    buffer.AppendLine();
                }
                buffer.Append(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (buffer.Length > 0)
                {
                    var text = buffer.ToString().TrimEnd();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yieldReturn.Add((currentStyle, text));
                    }
                    buffer.Clear();
                    currentStyle = null;
                }
                continue;
            }

            if (line.StartsWith("#"))
            {
                if (buffer.Length > 0)
                {
                    var headingText = buffer.ToString().TrimEnd();
                    if (!string.IsNullOrWhiteSpace(headingText))
                    {
                        yieldReturn.Add((currentStyle, headingText));
                    }
                    buffer.Clear();
                    currentStyle = null;
                }

                var level = line.TakeWhile(c => c == '#').Count();
                var headingContent = line[level..].Trim();
                yieldReturn.Add(($"Heading{Math.Clamp(level, 1, 6)}", headingContent));
                continue;
            }

            if (line.StartsWith(">"))
            {
                if (currentStyle != "Blockquote" && buffer.Length > 0)
                {
                    var text = buffer.ToString().TrimEnd();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yieldReturn.Add((currentStyle, text));
                    }
                    buffer.Clear();
                }

                currentStyle = "Blockquote";
                var quoteText = line.TrimStart('>', ' ').Trim();
                if (buffer.Length > 0)
                {
                    buffer.Append(' ');
                }
                buffer.Append(quoteText);
                continue;
            }

            if (currentStyle == "Blockquote" && !line.StartsWith(">"))
            {
                var text = buffer.ToString().TrimEnd();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yieldReturn.Add((currentStyle, text));
                }
                buffer.Clear();
                currentStyle = null;
            }

            currentStyle ??= "Normal";
            if (buffer.Length > 0)
            {
                buffer.Append(' ');
            }
            buffer.Append(line.Trim());
        }

        if (buffer.Length > 0)
        {
            var text = buffer.ToString().TrimEnd();
            if (!string.IsNullOrWhiteSpace(text))
            {
                yieldReturn.Add((inCodeBlock ? "CodeBlock" : currentStyle, text));
            }
        }

        return yieldReturn;
    }
}
