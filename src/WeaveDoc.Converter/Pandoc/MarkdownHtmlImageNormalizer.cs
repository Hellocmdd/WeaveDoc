using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WeaveDoc.Converter.Pandoc;

internal static class MarkdownHtmlImageNormalizer
{
    private static readonly Regex CenteredImageBlockRegex =
        new(
            @"<div\b[^>]*>\s*(<img\b[^>]*?/?>)\s*</div>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ImageTagRegex =
        new(@"<img\b[^>]*?/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttributeRegex =
        new(
            @"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'=<>`]+))",
            RegexOptions.Compiled);

    public static string NormalizeHtmlImages(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new StringBuilder(markdown.Length);
        var pending = new StringBuilder();
        var inFence = false;
        string? fenceMarker = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (StartsFence(trimmed, out var marker))
            {
                FlushPending(result, pending);
                if (!inFence)
                {
                    inFence = true;
                    fenceMarker = marker;
                }
                else if (marker == fenceMarker)
                {
                    inFence = false;
                    fenceMarker = null;
                }

                AppendLine(result, line, i, lines.Length);
                continue;
            }

            if (inFence)
            {
                AppendLine(result, line, i, lines.Length);
                continue;
            }

            pending.Append(line);
            if (i < lines.Length - 1)
                pending.Append('\n');
        }

        FlushPending(result, pending);
        var normalized = result.ToString();
        return markdown.Contains("\r\n", StringComparison.Ordinal) ? normalized.Replace("\n", "\r\n") : normalized;
    }

    private static void FlushPending(StringBuilder result, StringBuilder pending)
    {
        if (pending.Length == 0)
            return;

        var text = pending.ToString();
        text = CenteredImageBlockRegex.Replace(text, match =>
        {
            var markdownImage = TryConvertImage(match.Groups[1].Value);
            return markdownImage == null ? match.Value : $"{Environment.NewLine}{markdownImage}{Environment.NewLine}";
        });
        text = ImageTagRegex.Replace(text, match => TryConvertImage(match.Value) ?? match.Value);

        result.Append(text);
        pending.Clear();
    }

    private static string? TryConvertImage(string imgTag)
    {
        var attributes = ParseAttributes(imgTag);
        if (!attributes.TryGetValue("src", out var src) || string.IsNullOrWhiteSpace(src))
            return null;

        attributes.TryGetValue("alt", out var alt);
        return $"![{EscapeAltText(alt ?? "")}](<{src.Trim()}>)";
    }

    private static Dictionary<string, string> ParseAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(tag))
        {
            var name = match.Groups["name"].Value;
            var value = WebUtility.HtmlDecode(match.Groups["value"].Value);
            attributes[name] = value;
        }

        return attributes;
    }

    private static string EscapeAltText(string text) =>
        text.Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

    private static bool StartsFence(string trimmedLine, out string marker)
    {
        marker = "";
        if (trimmedLine.StartsWith("```", StringComparison.Ordinal))
        {
            marker = "```";
            return true;
        }

        if (trimmedLine.StartsWith("~~~", StringComparison.Ordinal))
        {
            marker = "~~~";
            return true;
        }

        return false;
    }

    private static void AppendLine(StringBuilder builder, string line, int index, int lineCount)
    {
        builder.Append(line);
        if (index < lineCount - 1)
            builder.Append('\n');
    }
}
