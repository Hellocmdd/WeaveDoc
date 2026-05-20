using System.Text;
using System.Text.RegularExpressions;
using WeaveDoc.Rag.Models;

namespace WeaveDoc.Rag.Services;

public sealed partial class LocalAiService
{
    private IReadOnlyList<DocumentChunk> SplitIntoChunks(string content, string sourceName, string relativePath)
    {
        var normalized = SanitizeMarkdownForIndex(content.Replace("\r\n", "\n", StringComparison.Ordinal));
        var chunks = new List<DocumentChunk>();

        var documentTitle = ExtractDocumentTitle(normalized, sourceName);
        var sectionTitle = documentTitle;
        var structurePath = documentTitle;
        var headingStack = new List<string>();
        var chunkIndex = 0;
        var buffer = new StringBuilder();
        var pendingOverlap = string.Empty;

        foreach (var paragraph in SplitParagraphs(normalized))
        {
            if (IsHeadingParagraph(paragraph))
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);
                pendingOverlap = string.Empty;
                sectionTitle = NormalizeMarkdownHeading(paragraph);
                structurePath = UpdateMarkdownHeadingPath(headingStack, paragraph, sectionTitle, documentTitle);
                buffer.AppendLine(paragraph.Trim());
                continue;
            }

            var trimmedParagraph = paragraph.Trim();
            if (buffer.Length > 0 && buffer.Length + trimmedParagraph.Length + 2 > _options.ChunkSize)
            {
                pendingOverlap = FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: true);
            }

            if (trimmedParagraph.Length > _options.ChunkSize)
            {
                FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);
                pendingOverlap = string.Empty;
                foreach (var oversizedPart in SplitLargeParagraph(trimmedParagraph))
                {
                    var oversizedKind = ResolveMarkdownContentKind(sectionTitle, structurePath, oversizedPart);
                    chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, oversizedPart, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, oversizedKind));
                }

                continue;
            }

            AppendPendingOverlap(buffer, pendingOverlap, trimmedParagraph.Length);
            pendingOverlap = string.Empty;

            if (buffer.Length > 0)
            {
                buffer.AppendLine();
            }

            buffer.Append(trimmedParagraph);
        }

        FlushChunkBuffer(chunks, buffer, sourceName, relativePath, documentTitle, sectionTitle, structurePath, ref chunkIndex, allowOverlapForNextChunk: false);

        return chunks;
    }

    private static string SanitizeMarkdownForIndex(string content)
    {
        var cleanedLines = new List<string>();
        foreach (var rawLine in content.Split('\n', StringSplitOptions.None))
        {
            var line = MarkdownImageRegex.Replace(rawLine, string.Empty);
            line = MarkdownLinkRegex.Replace(line, "$1").TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                cleanedLines.Add(string.Empty);
                continue;
            }

            var trimmed = line.Trim();
            if (HtmlTagRegex.IsMatch(trimmed) && HtmlTagRegex.Replace(trimmed, string.Empty).Trim().Length == 0)
            {
                continue;
            }

            line = HtmlTagRegex.Replace(line, string.Empty).TrimEnd();
            if (string.IsNullOrWhiteSpace(line)
                || IsMetadataLikeSentence(line)
                || IsReferenceLikeSentence(line)
                || IsTitleOrAuthorLikeLine(line))
            {
                continue;
            }

            cleanedLines.Add(line);
        }

        return string.Join('\n', cleanedLines);
    }

    private static string NormalizeMarkdownHeading(string paragraph)
    {
        return paragraph.Trim().TrimStart('#', ' ', '\t').Trim();
    }

    private static string UpdateMarkdownHeadingPath(List<string> headingStack, string paragraph, string sectionTitle, string documentTitle)
    {
        var level = GetMarkdownHeadingLevel(paragraph);
        if (level <= 0)
        {
            level = headingStack.Count == 0 ? 1 : Math.Min(headingStack.Count + 1, 6);
        }

        while (headingStack.Count >= level)
        {
            headingStack.RemoveAt(headingStack.Count - 1);
        }

        headingStack.Add(sectionTitle);
        return headingStack.Count == 0 ? documentTitle : string.Join(" > ", headingStack);
    }

    private static int GetMarkdownHeadingLevel(string paragraph)
    {
        var trimmed = paragraph.TrimStart();
        var count = 0;
        while (count < trimmed.Length && count < 6 && trimmed[count] == '#')
        {
            count++;
        }

        return count > 0 && count < trimmed.Length && char.IsWhiteSpace(trimmed[count]) ? count : 0;
    }

    private static string ExtractDocumentTitle(string content, string sourceName)
    {
        var firstHeading = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(IsHeadingParagraph);

        if (!string.IsNullOrWhiteSpace(firstHeading))
        {
            return firstHeading.TrimStart('#', ' ').Trim();
        }

        return Path.GetFileNameWithoutExtension(sourceName);
    }

    private static IEnumerable<string> SplitParagraphs(string content)
    {
        return content
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));
    }

    private static bool IsHeadingParagraph(string paragraph)
    {
        var trimmed = paragraph.Trim();
        return HeadingLineRegex.IsMatch(trimmed)
            || IsPlainTextSectionHeading(trimmed);
    }

    private static bool IsPlainTextSectionHeading(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 42)
        {
            return false;
        }

        if (text.Contains('。') || text.Contains('，') || text.Contains('.') || text.Contains(';') || text.Contains('；'))
        {
            return false;
        }

        if (IsMetadataLikeSentence(text) || IsReferenceLikeSentence(text) || IsTitleOrAuthorLikeLine(text))
        {
            return false;
        }

        if (text.StartsWith('|') || text.StartsWith('-') || text.StartsWith('*'))
        {
            return false;
        }

        var hasHeadingSignal = Regex.IsMatch(text, @"^\s*(?:第?[一二三四五六七八九十\d]+[章节条部分]?|[一二三四五六七八九十\d]+(?:\.\d+)*|附录|摘要|引言|结论|参考文献)(?:\s|、|：|:)")
            || CountSignals(text, "摘要", "引言", "绪论", "方法", "系统", "设计", "实现", "实验", "结果", "讨论", "结论", "模块", "架构", "流程") > 0;

        return hasHeadingSignal;
    }

    private string FlushChunkBuffer(
        List<DocumentChunk> chunks,
        StringBuilder buffer,
        string sourceName,
        string relativePath,
        string documentTitle,
        string sectionTitle,
        string structurePath,
        ref int chunkIndex,
        bool allowOverlapForNextChunk)
    {
        var text = buffer.ToString().Trim();
        buffer.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var contentKind = ResolveMarkdownContentKind(sectionTitle, structurePath, text);
        chunks.Add(new DocumentChunk(sourceName, relativePath, chunkIndex++, text, Array.Empty<float>(), documentTitle, sectionTitle, structurePath, contentKind));
        return allowOverlapForNextChunk ? BuildChunkOverlapPrefix(text) : string.Empty;
    }

    private void AppendPendingOverlap(StringBuilder buffer, string pendingOverlap, int nextParagraphLength)
    {
        if (buffer.Length > 0 || string.IsNullOrWhiteSpace(pendingOverlap) || _options.ChunkOverlap <= 0)
        {
            return;
        }

        var maxAllowedLength = Math.Max(0, _options.ChunkSize - nextParagraphLength - 2);
        if (maxAllowedLength <= 0)
        {
            return;
        }

        var overlap = pendingOverlap.Trim();
        if (overlap.Length > maxAllowedLength)
        {
            overlap = overlap[^maxAllowedLength..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(overlap))
        {
            buffer.Append(overlap);
        }
    }

    private string BuildChunkOverlapPrefix(string chunkText)
    {
        if (_options.ChunkOverlap <= 0 || string.IsNullOrWhiteSpace(chunkText))
        {
            return string.Empty;
        }

        var normalized = chunkText.Trim();
        var overlapLength = Math.Min(_options.ChunkOverlap, normalized.Length);
        if (overlapLength <= 0)
        {
            return string.Empty;
        }

        return normalized[^overlapLength..].Trim();
    }

    private IEnumerable<string> SplitLargeParagraph(string paragraph)
    {
        var normalized = paragraph.Trim();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(_options.ChunkSize, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var lastBreak = normalized.LastIndexOfAny(['。', '！', '？', '\n'], end - 1, length);
                if (lastBreak > start + (_options.ChunkSize / 2))
                {
                    end = lastBreak + 1;
                }
            }

            var text = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return text;
            }

            if (end >= normalized.Length)
            {
                break;
            }

            var nextStart = Math.Max(0, end - _options.ChunkOverlap);
            start = nextStart > start ? nextStart : end;
        }
    }
}
