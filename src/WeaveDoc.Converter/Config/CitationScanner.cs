using System.Text;
using System.Text.RegularExpressions;

namespace WeaveDoc.Converter.Config;

/// <summary>扫描结果：去重后的 key 列表（首次出现顺序）+ 每处出现。</summary>
public record CitationScanResult(
    IReadOnlyList<string> Keys,
    IReadOnlyList<CitationOccurrence> Occurrences);

public record CitationOccurrence(int Index, string Key, int StartPosition);

/// <summary>
/// 从 Markdown 文本提取 Pandoc citation key（[@key] 语法）。
/// 保持首次出现顺序、去重。排除代码块/行内代码（用文本掩码：把代码区域原地替换为等长空格）。
/// </summary>
public class CitationScanner
{
    // (?<!\\) 排除转义 \@；(?<![A-Za-z0-9]) 确保 @ 在词边界（排除 user@host 邮箱）；
    // -? 吃掉负引用 -@key；捕获组匹配 key 字符集（须以字母/数字开头）
    private static readonly Regex CitationRegex = new(
        @"(?<!\\)(?<![A-Za-z0-9])-?@([A-Za-z0-9][A-Za-z0-9_.:+-]*)",
        RegexOptions.Compiled);

    // 围栏代码块：``` 或 ~~~ 起始行到对应结束围栏
    private static readonly Regex FencedBlockRegex = new(
        @"(?m)^[ \t]*(```+|~~~+)[^\n]*\n([\s\S]*?)^[ \t]*\1[ \t]*$",
        RegexOptions.Compiled);

    public CitationScanResult Scan(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return new CitationScanResult(Array.Empty<string>(), Array.Empty<CitationOccurrence>());

        var masked = MaskCode(markdown);
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occurrences = new List<CitationOccurrence>();

        foreach (Match match in CitationRegex.Matches(masked))
        {
            var key = match.Groups[1].Value;
            if (seen.Add(key))
            {
                occurrences.Add(new CitationOccurrence(keys.Count, key, match.Index));
                keys.Add(key);
            }
        }

        return new CitationScanResult(keys, occurrences);
    }

    /// <summary>
    /// 把代码区域（围栏代码块、缩进代码块、行内代码反引号、转义引用括号）原地替换为等长空格，
    /// 保持字符偏移不变，使正则在掩码文本上扫描时自然跳过这些区域的 @key。
    /// </summary>
    private static string MaskCode(string markdown)
    {
        var buffer = new StringBuilder(markdown);

        // 0. 转义的引用括号：\[@ ... \] 整段掩码（Pandoc 中 \[@key] 是字面量，非引用）
        MaskEscapedCitations(buffer);

        // 1. 围栏代码块（``` / ~~~）：整块（含围栏行）替换为空格，保留换行
        buffer = MaskMatches(buffer, FencedBlockRegex);

        // 2. 缩进代码块（行首 4+ 空格或 1 tab）：逐行掩码
        MaskIndentedCodeBlocks(buffer);

        // 3. 行内代码（反引号）：`...` 或 ``...`` 配对，内容掩码（反引号本身也掩码）
        MaskInlineCode(buffer);

        return buffer.ToString();
    }

    private static void MaskEscapedCitations(StringBuilder buffer)
    {
        var text = buffer.ToString();
        int i = 0;
        while (i < text.Length)
        {
            // 匹配 \[@
            if (i + 2 < text.Length && text[i] == '\\' && text[i + 1] == '[' && text[i + 2] == '@')
            {
                // 掩码 \[ ，然后找到匹配的 \]（未转义的 ]）
                buffer[i] = ' ';
                buffer[i + 1] = ' ';
                int j = i + 2;
                while (j < text.Length)
                {
                    if (text[j] == ']' && (j == 0 || text[j - 1] != '\\'))
                    {
                        buffer[j] = ' ';
                        break;
                    }
                    if (text[j] != '\n' && text[j] != '\r')
                        buffer[j] = ' ';
                    j++;
                }
                i = j + 1;
            }
            else
            {
                i++;
            }
        }
    }

    private static StringBuilder MaskMatches(StringBuilder buffer, Regex regex)
    {
        // 在副本字符串上匹配，回写 buffer（偏移因等长替换保持不变）
        var text = buffer.ToString();
        foreach (Match m in regex.Matches(text))
        {
            for (int i = m.Index; i < m.Index + m.Length; i++)
            {
                if (text[i] != '\n' && text[i] != '\r')
                    buffer[i] = ' ';
            }
        }
        return buffer;
    }

    private static void MaskIndentedCodeBlocks(StringBuilder buffer)
    {
        var text = buffer.ToString();
        var lines = text.Split('\n');
        var offset = 0;
        var inFenced = false;
        string? fenceMarker = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine;
            var lineLen = line.Length;
            var trimmedStart = line.TrimStart();

            // 跟踪围栏状态（围栏内的缩进行不算代码块）
            if (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~"))
            {
                var marker = trimmedStart.StartsWith("```") ? "```" : "~~~";
                if (!inFenced) { inFenced = true; fenceMarker = marker; }
                else if (fenceMarker != null && trimmedStart.StartsWith(fenceMarker)) { inFenced = false; fenceMarker = null; }
            }
            else if (!inFenced && (line.StartsWith("    ") || line.StartsWith("\t")))
            {
                // 缩进代码行：掩码整行（保留换行由外层处理）
                for (int i = offset; i < offset + lineLen; i++)
                {
                    if (buffer[i] != '\n' && buffer[i] != '\r')
                        buffer[i] = ' ';
                }
            }
            offset += lineLen + 1; // +1 for '\n'
        }
    }

    private static void MaskInlineCode(StringBuilder buffer)
    {
        var text = buffer.ToString();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '`')
            {
                i++;
                continue;
            }

            // 数开头的连续反引号个数（支持 ``code``）
            int tickCount = 0;
            int start = i;
            while (i < text.Length && text[i] == '`')
            {
                tickCount++;
                i++;
            }

            // 寻找等长的闭合反引号串
            int contentStart = i;
            int closeIndex = -1;
            while (i < text.Length)
            {
                if (text[i] == '`')
                {
                    int cnt = 0;
                    int j = i;
                    while (j < text.Length && text[j] == '`') { cnt++; j++; }
                    if (cnt == tickCount)
                    {
                        closeIndex = i;
                        break;
                    }
                    i = j;
                }
                else
                {
                    i++;
                }
            }

            if (closeIndex < 0)
                continue; // 未闭合，按普通文本处理

            // 掩码从开反引号到闭反引号结束（含反引号本身），保留换行
            for (int k = start; k < closeIndex + tickCount && k < buffer.Length; k++)
            {
                if (buffer[k] != '\n' && buffer[k] != '\r')
                    buffer[k] = ' ';
            }
            i = closeIndex + tickCount;
        }
    }
}
