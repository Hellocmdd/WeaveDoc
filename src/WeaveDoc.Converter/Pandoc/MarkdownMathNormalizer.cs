namespace WeaveDoc.Converter.Pandoc;

internal static class MarkdownMathNormalizer
{
    public static string NormalizeDollarMath(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inFence = false;
        string? fenceMarker = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (StartsFence(trimmed, out var marker))
            {
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

                continue;
            }

            if (!inFence)
                lines[i] = NormalizeLine(lines[i]);
        }

        var normalized = string.Join('\n', lines);
        return markdown.Contains("\r\n", StringComparison.Ordinal) ? normalized.Replace("\n", "\r\n") : normalized;
    }

    private static string NormalizeLine(string line)
    {
        var result = new System.Text.StringBuilder(line.Length);
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '`')
            {
                var end = FindInlineCodeEnd(line, i);
                if (end > i)
                {
                    result.Append(line, i, end - i + 1);
                    i = end + 1;
                    continue;
                }
            }

            if (line[i] == '$' && !IsEscaped(line, i) && !IsDoubleDollar(line, i))
            {
                var end = FindClosingDollar(line, i + 1);
                if (end > i)
                {
                    var inner = line[(i + 1)..end];
                    var trimmed = inner.Trim();
                    if (trimmed.Length > 0)
                    {
                        result.Append('$');
                        result.Append(trimmed);
                        result.Append('$');
                        i = end + 1;
                        continue;
                    }
                }
            }

            result.Append(line[i]);
            i++;
        }

        return result.ToString();
    }

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

    private static int FindInlineCodeEnd(string line, int start)
    {
        var tickCount = 0;
        while (start + tickCount < line.Length && line[start + tickCount] == '`')
            tickCount++;

        for (var i = start + tickCount; i <= line.Length - tickCount; i++)
        {
            var matched = true;
            for (var j = 0; j < tickCount; j++)
            {
                if (line[i + j] != '`')
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return i + tickCount - 1;
        }

        return -1;
    }

    private static int FindClosingDollar(string line, int start)
    {
        for (var i = start; i < line.Length; i++)
        {
            if (line[i] == '$' && !IsEscaped(line, i) && !IsDoubleDollar(line, i))
                return i;
        }

        return -1;
    }

    private static bool IsDoubleDollar(string line, int index) =>
        (index > 0 && line[index - 1] == '$') || (index + 1 < line.Length && line[index + 1] == '$');

    private static bool IsEscaped(string line, int index)
    {
        var slashCount = 0;
        for (var i = index - 1; i >= 0 && line[i] == '\\'; i--)
            slashCount++;

        return slashCount % 2 == 1;
    }
}
