using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WeaveDoc.Converter;

namespace WeaveDoc.Converter.Pandoc;

public class MarkdownPreprocessor
{
    private const long MaxRemoteImageBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan RemoteImageTimeout = TimeSpan.FromSeconds(10);

    private static readonly Regex FenceRegex = new(@"^\s{0,3}(```+|~~~+)", RegexOptions.Compiled);
    private static readonly Regex HtmlTableRegex = new(@"<table\b[^>]*>.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlRowRegex = new(@"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlCellRegex = new(@"<(th|td)\b[^>]*>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MarkdownRemoteImageRegex = new(@"!\[([^\]]*)\]\((https?://[^\s)]+)(?:\s+""[^""]*"")?\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlRemoteImageRegex = new(@"<img\b[^>]*\bsrc\s*=\s*(['""])(https?://.*?)\1[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AltAttributeRegex = new(@"\balt\s*=\s*(['""])(.*?)\1", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = RemoteImageTimeout
    };

    public async Task<MarkdownPreprocessResult> PreprocessAsync(
        string inputPath,
        string tempDir,
        CancellationToken ct = default)
    {
        var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Directory.GetCurrentDirectory();
        var remoteMediaDir = Path.Combine(tempDir, "remote-media");
        var warnings = new List<ConversionWarning>();
        var downloadedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var markdown = await File.ReadAllTextAsync(inputPath, ct);
        var processed = await ProcessOutsideCodeFencesAsync(
            markdown,
            remoteMediaDir,
            downloadedImages,
            warnings,
            ct);

        Directory.CreateDirectory(tempDir);
        var preprocessedPath = Path.Combine(tempDir, "preprocessed.md");
        await File.WriteAllTextAsync(preprocessedPath, processed, Encoding.UTF8, ct);

        var resourcePaths = new List<string> { inputDirectory };
        if (Directory.Exists(remoteMediaDir))
            resourcePaths.Add(remoteMediaDir);

        return new MarkdownPreprocessResult(preprocessedPath, resourcePaths, warnings);
    }

    private static async Task<string> ProcessOutsideCodeFencesAsync(
        string markdown,
        string remoteMediaDir,
        Dictionary<string, string> downloadedImages,
        List<ConversionWarning> warnings,
        CancellationToken ct)
    {
        var builder = new StringBuilder();
        var segment = new StringBuilder();
        var inFence = false;

        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var isFenceLine = FenceRegex.IsMatch(line);
            if (isFenceLine)
            {
                if (!inFence)
                {
                    builder.Append(await ProcessContentSegmentAsync(segment.ToString(), remoteMediaDir, downloadedImages, warnings, ct));
                    segment.Clear();
                    inFence = true;
                }
                else
                {
                    inFence = false;
                }

                builder.Append(line).Append('\n');
                continue;
            }

            if (inFence)
                builder.Append(line).Append('\n');
            else
                segment.Append(line).Append('\n');
        }

        if (segment.Length > 0)
            builder.Append(await ProcessContentSegmentAsync(segment.ToString(), remoteMediaDir, downloadedImages, warnings, ct));

        return builder.ToString();
    }

    private static async Task<string> ProcessContentSegmentAsync(
        string segment,
        string remoteMediaDir,
        Dictionary<string, string> downloadedImages,
        List<ConversionWarning> warnings,
        CancellationToken ct)
    {
        var withTables = ProcessHtmlTables(segment, warnings);
        return await ProcessRemoteImagesAsync(withTables, remoteMediaDir, downloadedImages, warnings, ct);
    }

    private static string ProcessHtmlTables(string markdown, List<ConversionWarning> warnings)
    {
        return HtmlTableRegex.Replace(markdown, match =>
        {
            var html = match.Value;
            if (Regex.Matches(html, @"<table\b", RegexOptions.IgnoreCase).Count > 1)
            {
                warnings.Add(new ConversionWarning("html-table.unsupported", "HTML 表格包含嵌套 table，已保留原文。", "html-table"));
                return html;
            }

            if (Regex.IsMatch(html, @"\b(rowspan|colspan)\s*=", RegexOptions.IgnoreCase))
            {
                warnings.Add(new ConversionWarning("html-table.unsupported", "HTML 表格包含 rowspan/colspan，已保留原文。", "html-table"));
                return html;
            }

            var rows = HtmlRowRegex.Matches(html);
            if (rows.Count == 0)
            {
                warnings.Add(new ConversionWarning("html-table.unparsed", "HTML 表格未提取到 tr 行，已保留原文。", "html-table"));
                return html;
            }

            var tableRows = new List<List<string>>();
            var firstRowHasHeader = false;
            foreach (Match rowMatch in rows)
            {
                var cells = HtmlCellRegex.Matches(rowMatch.Groups[1].Value);
                if (cells.Count == 0)
                    continue;

                var row = new List<string>();
                foreach (Match cell in cells)
                {
                    if (tableRows.Count == 0 && string.Equals(cell.Groups[1].Value, "th", StringComparison.OrdinalIgnoreCase))
                        firstRowHasHeader = true;
                    row.Add(CleanTableCell(cell.Groups[2].Value));
                }

                tableRows.Add(row);
            }

            if (tableRows.Count == 0 || tableRows[0].Count == 0)
            {
                warnings.Add(new ConversionWarning("html-table.unparsed", "HTML 表格未提取到有效单元格，已保留原文。", "html-table"));
                return html;
            }

            var columnCount = tableRows[0].Count;
            if (tableRows.Any(row => row.Count != columnCount))
            {
                warnings.Add(new ConversionWarning("html-table.unsupported", "HTML 表格列数不一致，已保留原文。", "html-table"));
                return html;
            }

            if (!firstRowHasHeader)
            {
                warnings.Add(new ConversionWarning("html-table.assumed-header", "HTML 表格没有 th，已将第一行作为 Markdown 表头。", "html-table"));
            }

            return "\n\n" + BuildPipeTable(tableRows) + "\n\n";
        });
    }

    private static async Task<string> ProcessRemoteImagesAsync(
        string markdown,
        string remoteMediaDir,
        Dictionary<string, string> downloadedImages,
        List<ConversionWarning> warnings,
        CancellationToken ct)
    {
        var afterMarkdownImages = await ReplaceAsync(
            markdown,
            MarkdownRemoteImageRegex,
            async match =>
            {
                var alt = WebUtility.HtmlDecode(match.Groups[1].Value);
                var url = match.Groups[2].Value;
                var localPath = await DownloadRemoteImageAsync(url, remoteMediaDir, downloadedImages, warnings, ct);
                return localPath == null
                    ? DowngradeImageAltText(alt)
                    : $"![{EscapeMarkdownAlt(alt)}]({NormalizeMarkdownPath(localPath)})";
            });

        return await ReplaceAsync(
            afterMarkdownImages,
            HtmlRemoteImageRegex,
            async match =>
            {
                var url = match.Groups[2].Value;
                var alt = ExtractAltText(match.Value);
                var localPath = await DownloadRemoteImageAsync(url, remoteMediaDir, downloadedImages, warnings, ct);
                return localPath == null
                    ? DowngradeImageAltText(alt)
                    : $"![{EscapeMarkdownAlt(alt)}]({NormalizeMarkdownPath(localPath)})";
            });
    }

    private static async Task<string?> DownloadRemoteImageAsync(
        string url,
        string remoteMediaDir,
        Dictionary<string, string> downloadedImages,
        List<ConversionWarning> warnings,
        CancellationToken ct)
    {
        if (downloadedImages.TryGetValue(url, out var existingPath))
            return existingPath;

        try
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                warnings.Add(new ConversionWarning("remote-image.download-failed", $"远程图片下载失败: HTTP {(int)response.StatusCode}", url));
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var bytes = await ReadLimitedAsync(stream, MaxRemoteImageBytes, ct);
            var extension = DetectImageExtension(bytes);
            if (extension == null)
            {
                warnings.Add(new ConversionWarning("remote-image.invalid-image", "远程资源不是支持的图片格式，已降级为文本。", url));
                return null;
            }

            Directory.CreateDirectory(remoteMediaDir);
            var localPath = Path.Combine(remoteMediaDir, $"{HashUrl(url)}{extension}");
            await File.WriteAllBytesAsync(localPath, bytes, ct);
            downloadedImages[url] = localPath;
            return localPath;
        }
        catch (InvalidDataException ex)
        {
            warnings.Add(new ConversionWarning("remote-image.too-large", ex.Message, url));
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            warnings.Add(new ConversionWarning("remote-image.download-failed", $"远程图片下载失败: {ex.Message}", url));
            return null;
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, long maxBytes, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            if (memory.Length + read > maxBytes)
                throw new InvalidDataException($"远程图片超过大小限制 {maxBytes / 1024 / 1024} MB，已降级为文本。");
            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static string? DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return ".png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ".jpg";
        if (bytes.Length >= 6 && Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a")
            return ".gif";
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return ".bmp";
        if (bytes.Length >= 4
            && ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00)
                || (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A)))
            return ".tiff";

        return null;
    }

    private static string BuildPipeTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        AppendPipeRow(builder, rows[0]);
        AppendPipeRow(builder, Enumerable.Repeat("---", rows[0].Count).ToList());
        for (var i = 1; i < rows.Count; i++)
            AppendPipeRow(builder, rows[i]);
        return builder.ToString().TrimEnd();
    }

    private static void AppendPipeRow(StringBuilder builder, IReadOnlyList<string> cells)
    {
        builder.Append("| ");
        builder.Append(string.Join(" | ", cells.Select(EscapePipeCell)));
        builder.Append(" |\n");
    }

    private static string CleanTableCell(string html)
    {
        var withoutTags = HtmlTagRegex.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static string EscapePipeCell(string value) =>
        string.IsNullOrEmpty(value) ? " " : value.Replace("|", "\\|");

    private static string ExtractAltText(string imgTag)
    {
        var match = AltAttributeRegex.Match(imgTag);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[2].Value) : string.Empty;
    }

    private static string DowngradeImageAltText(string alt) =>
        string.IsNullOrWhiteSpace(alt) ? "[图片下载失败]" : alt;

    private static string EscapeMarkdownAlt(string alt) =>
        alt.Replace("[", "\\[").Replace("]", "\\]");

    private static string NormalizeMarkdownPath(string path) =>
        path.Replace('\\', '/');

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static async Task<string> ReplaceAsync(
        string input,
        Regex regex,
        Func<Match, Task<string>> replacement)
    {
        var builder = new StringBuilder();
        var lastIndex = 0;
        foreach (Match match in regex.Matches(input))
        {
            builder.Append(input, lastIndex, match.Index - lastIndex);
            builder.Append(await replacement(match));
            lastIndex = match.Index + match.Length;
        }

        builder.Append(input, lastIndex, input.Length - lastIndex);
        return builder.ToString();
    }
}
