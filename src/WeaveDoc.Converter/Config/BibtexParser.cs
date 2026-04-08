namespace WeaveDoc.Converter.Config;

/// <summary>
/// BibTeX 文献解析器（实用级）
/// </summary>
public class BibtexParser
{
    public List<BibtexEntry> Parse(string bibContent)
    {
        var entries = new List<BibtexEntry>();
        if (string.IsNullOrWhiteSpace(bibContent))
            return entries;

        var abbreviations = ParseStringDefinitions(bibContent);
        var i = 0;

        while (i < bibContent.Length)
        {
            // 找到下一个 @
            var atIndex = bibContent.IndexOf('@', i);
            if (atIndex < 0) break;

            // 读取 entry type
            var typeStart = atIndex + 1;
            var typeEnd = typeStart;
            while (typeEnd < bibContent.Length && char.IsLetter(bibContent[typeEnd]))
                typeEnd++;

            var entryType = bibContent[typeStart..typeEnd].ToLowerInvariant();

            // 跳过 @comment 和 @preamble
            if (entryType is "comment" or "preamble")
            {
                i = typeEnd;
                continue;
            }

            // 跳过 @string（已处理）
            if (entryType == "string")
            {
                i = typeEnd;
                continue;
            }

            // 找到开括号
            var openBrace = bibContent.IndexOf('{', typeEnd);
            if (openBrace < 0) break;

            // 用括号计数器找到匹配的闭括号
            var closeBrace = FindMatchingBrace(bibContent, openBrace);
            if (closeBrace < 0) break;

            var body = bibContent[(openBrace + 1)..closeBrace];
            var entry = ParseEntryBody(entryType, body, abbreviations);
            if (entry != null)
                entries.Add(entry);

            i = closeBrace + 1;
        }

        return entries;
    }

    public BibtexEntry? ParseSingle(string entryText)
    {
        var entries = Parse(entryText);
        return entries.Count > 0 ? entries[0] : null;
    }

    private Dictionary<string, string> ParseStringDefinitions(string content)
    {
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < content.Length)
        {
            var atIndex = content.IndexOf("@string", i, StringComparison.OrdinalIgnoreCase);
            if (atIndex < 0) break;

            var openBrace = content.IndexOf('{', atIndex);
            if (openBrace < 0) break;

            var closeBrace = FindMatchingBrace(content, openBrace);
            if (closeBrace < 0) break;

            var body = content[(openBrace + 1)..closeBrace].Trim();
            var eqIndex = body.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = body[..eqIndex].Trim();
                var valuePart = body[(eqIndex + 1)..].Trim();
                var value = ExtractFieldValue(valuePart, abbreviations);
                if (value != null)
                    abbreviations[key] = value;
            }

            i = closeBrace + 1;
        }

        return abbreviations;
    }

    private static int FindMatchingBrace(string text, int openPos)
    {
        var depth = 0;
        for (var i = openPos; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private BibtexEntry? ParseEntryBody(string entryType, string body,
        Dictionary<string, string> abbreviations)
    {
        // 提取 citation key：逗号前的第一个 token
        var commaIndex = body.IndexOf(',');
        if (commaIndex < 0) return null;

        var citationKey = body[..commaIndex].Trim();
        if (string.IsNullOrEmpty(citationKey)) return null;

        var fieldsText = body[(commaIndex + 1)..];
        var fields = ParseFields(fieldsText, abbreviations);

        return new BibtexEntry
        {
            EntryType = entryType,
            CitationKey = citationKey,
            Fields = fields
        };
    }

    private Dictionary<string, string> ParseFields(string text,
        Dictionary<string, string> abbreviations)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;

        while (i < text.Length)
        {
            // 跳过空白和逗号
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ','))
                i++;

            if (i >= text.Length) break;

            // 读取字段名
            var nameStart = i;
            while (i < text.Length && text[i] != '=' && !char.IsWhiteSpace(text[i]))
                i++;

            var fieldName = text[nameStart..i].Trim();
            if (string.IsNullOrEmpty(fieldName)) break;

            // 跳过空白到 =
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '='))
                i++;

            if (i >= text.Length) break;

            // 提取值
            var value = ExtractFieldValue(text[i..], abbreviations, out var consumed);
            if (value != null)
                fields[fieldName] = value;

            i += consumed;
        }

        return fields;
    }

    private string? ExtractFieldValue(string text,
        Dictionary<string, string> abbreviations)
    {
        return ExtractFieldValue(text, abbreviations, out _);
    }

    private string? ExtractFieldValue(string text,
        Dictionary<string, string> abbreviations, out int consumed)
    {
        consumed = 0;
        if (string.IsNullOrEmpty(text)) return null;

        var trimmed = text.TrimStart();
        var leadingWhitespace = text.Length - trimmed.Length;

        if (trimmed.Length == 0) return null;

        if (trimmed[0] == '{')
        {
            // 大括号值
            var closeBrace = FindMatchingBrace(trimmed, 0);
            if (closeBrace < 0) return null;

            consumed = leadingWhitespace + closeBrace + 1;
            return trimmed[1..closeBrace];
        }

        if (trimmed[0] == '"')
        {
            // 引号值
            var closeQuote = trimmed.IndexOf('"', 1);
            if (closeQuote < 0) return null;

            consumed = leadingWhitespace + closeQuote + 1;
            return trimmed[1..closeQuote];
        }

        // 裸字（可能带 # 拼接）
        var end = 0;
        while (end < trimmed.Length &&
               trimmed[end] != ',' && trimmed[end] != '}' &&
               !char.IsWhiteSpace(trimmed[end]))
            end++;

        if (end == 0) return null;

        consumed = leadingWhitespace + end;
        var bareWord = trimmed[..end].Trim();

        // 查找缩写映射
        return abbreviations.TryGetValue(bareWord, out var expanded)
            ? expanded : bareWord;
    }
}

public record BibtexEntry
{
    public string EntryType { get; init; } = "";     // article, book, inproceedings 等
    public string CitationKey { get; init; } = "";    // @article{this_part,
    public Dictionary<string, string> Fields { get; init; } = new();
}
