using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WeaveDoc.Converter.Pandoc;

internal static class MarkdownHtmlTableNormalizer
{
    private static readonly Regex HtmlTableRegex =
        new(@"<table\b[^>]*>.*?</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string NormalizeHtmlTables(string markdown)
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
        result.Append(HtmlTableRegex.Replace(text, match =>
        {
            var markdownTable = TryConvertTable(match.Value);
            return markdownTable ?? match.Value;
        }));
        pending.Clear();
    }

    private static string? TryConvertTable(string html)
    {
        try
        {
            var root = XDocument.Parse($"<root>{html}</root>").Root;
            var table = root?.Elements().FirstOrDefault(e => IsElement(e, "table"));
            if (table == null)
                return null;

            var rows = ExtractRows(table);
            if (rows.Count == 0 || rows.All(row => row.Count == 0))
                return null;

            var columnCount = rows.Max(row => row.Count);
            if (columnCount == 0)
                return null;

            foreach (var row in rows)
            {
                while (row.Count < columnCount)
                    row.Add("");
            }

            return BuildPipeTable(rows);
        }
        catch
        {
            return null;
        }
    }

    private static List<List<string>> ExtractRows(XElement table)
    {
        var rows = new List<List<string>>();
        var rowSpans = new Dictionary<int, PendingRowSpan>();

        foreach (var tr in table.Descendants().Where(e => IsElement(e, "tr")))
        {
            var row = new List<string>();
            var columnIndex = 0;

            foreach (var cell in tr.Elements().Where(e => IsElement(e, "td") || IsElement(e, "th")))
            {
                AppendPendingRowSpans(row, rowSpans, ref columnIndex);

                var text = NormalizeCellText(cell.Value);
                row.Add(text);

                var rowspan = ParseSpan(cell.Attribute("rowspan")?.Value);
                if (rowspan > 1)
                    rowSpans[columnIndex] = new PendingRowSpan(text, rowspan - 1);

                columnIndex++;
            }

            AppendPendingRowSpans(row, rowSpans, ref columnIndex);
            rows.Add(row);
        }

        return rows;
    }

    private static void AppendPendingRowSpans(
        List<string> row,
        Dictionary<int, PendingRowSpan> rowSpans,
        ref int columnIndex)
    {
        while (rowSpans.TryGetValue(columnIndex, out var span))
        {
            row.Add(span.Text);
            var next = span with { RemainingRows = span.RemainingRows - 1 };
            if (next.RemainingRows <= 0)
                rowSpans.Remove(columnIndex);
            else
                rowSpans[columnIndex] = next;

            columnIndex++;
        }
    }

    private static string BuildPipeTable(List<List<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine(ToPipeRow(rows[0]));
        builder.AppendLine(ToPipeRow(rows[0].Select(_ => "---").ToList()));

        foreach (var row in rows.Skip(1))
            builder.AppendLine(ToPipeRow(row));

        builder.AppendLine();
        return builder.ToString();
    }

    private static string ToPipeRow(IReadOnlyList<string> cells) =>
        "| " + string.Join(" | ", cells.Select(EscapePipeCell)) + " |";

    private static string EscapePipeCell(string cell) =>
        cell.Replace("|", "\\|", StringComparison.Ordinal);

    private static string NormalizeCellText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static int ParseSpan(string? value) =>
        int.TryParse(value, out var span) && span > 1 ? span : 1;

    private static bool IsElement(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

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

    private sealed record PendingRowSpan(string Text, int RemainingRows);
}
