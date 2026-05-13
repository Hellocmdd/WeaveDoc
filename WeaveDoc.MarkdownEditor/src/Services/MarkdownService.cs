using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace WeaveDoc.MarkdownEditor.Services
{
    public class MarkdownService
    {
        private static readonly Regex HeaderRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
        private static readonly Regex ListItemRegex = new Regex(@"^(\s*)([-*]|\d+\.)\s+(.+)$", RegexOptions.Multiline);
        private static readonly Regex BlockquoteRegex = new Regex(@"^>\s*(.+)$", RegexOptions.Multiline);
        private static readonly Regex CodeBlockRegex = new Regex(@"^```(\w*)$", RegexOptions.Multiline);
        private static readonly Regex BoldItalicRegex = new Regex(@"^\*\*\*(.+?)\*\*\*$");
        private static readonly Regex BoldRegex = new Regex(@"^\*\*(.+?)\*\*$");
        private static readonly Regex ItalicRegex = new Regex(@"^\*(.+?)\*$");
        private static readonly Regex InlineCodeRegex = new Regex(@"`([^`]+)`");
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^\)]+)\)");
        private static readonly Regex ImageRegex = new Regex(@"!\[([^\]]*)\]\(([^\)]+)\)");
        private static readonly Regex HrRegex = new Regex(@"^---+$", RegexOptions.Multiline);
        private static readonly Regex TableRowRegex = new Regex(@"^\|(.+)\|$");

        public string ConvertMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var lines = markdown.Split('\n');
            var result = new StringBuilder();
            bool inPre = false;
            string preLang = "";
            var preContent = new StringBuilder();
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var lineNumber = i + 1;
                var trimmed = line.Trim();

                if (CodeBlockRegex.IsMatch(trimmed))
                {
                    if (!inPre)
                    {
                        inPre = true;
                        var match = CodeBlockRegex.Match(trimmed);
                        preLang = match.Groups[1].Value;
                        preContent.Clear();
                        i++;
                        continue;
                    }
                    else
                    {
                        inPre = false;
                        result.Append($"<pre><code class=\"language-{EscapeHtml(preLang)}\" data-line=\"{lineNumber}\">{preContent.ToString()}</code></pre>\n");
                        preContent.Clear();
                        preLang = "";
                        i++;
                        continue;
                    }
                }

                if (inPre)
                {
                    preContent.AppendLine($"<span data-line=\"{lineNumber}\">{EscapeHtml(line)}</span>");
                    i++;
                    continue;
                }

                var listMatch = ListItemRegex.Match(trimmed);
                if (listMatch.Success)
                {
                    var tag = char.IsDigit(listMatch.Groups[2].Value[0]) ? "ol" : "ul";
                    int startLine = lineNumber;
                    result.Append($"<{tag}>\n");
                    
                    while (i < lines.Length)
                    {
                        line = lines[i];
                        lineNumber = i + 1;
                        trimmed = line.Trim();
                        
                        listMatch = ListItemRegex.Match(trimmed);
                        if (!listMatch.Success)
                            break;
                            
                        var content = ProcessInlineElements(listMatch.Groups[3].Value);
                        result.Append($"<li data-line=\"{lineNumber}\">{content}</li>\n");
                        i++;
                    }
                    
                    result.Append($"</{tag}>\n");
                    continue;
                }

                var html = ConvertLineToHtml(line, lineNumber);
                if (!string.IsNullOrEmpty(html))
                {
                    result.Append(html);
                }
                else
                {
                    result.Append($"<p data-line=\"{lineNumber}\">&nbsp;</p>\n");
                }

                i++;
            }

            if (inPre && preContent.Length > 0)
            {
                result.Append($"<pre><code class=\"language-{EscapeHtml(preLang)}\" data-line=\"{lines.Length}\">{preContent.ToString()}</code></pre>\n");
            }

            return result.ToString();
        }

        private string ConvertLineToHtml(string line, int lineNumber)
        {
            var trimmed = line.Trim();

            if (HrRegex.IsMatch(trimmed))
            {
                return $"<hr data-line=\"{lineNumber}\">\n";
            }

            var blockquoteMatch = BlockquoteRegex.Match(trimmed);
            if (blockquoteMatch.Success)
            {
                var content = ProcessInlineElements(blockquoteMatch.Groups[1].Value);
                return $"<blockquote data-line=\"{lineNumber}\">{content}</blockquote>\n";
            }

            var headerMatch = HeaderRegex.Match(trimmed);
            if (headerMatch.Success)
            {
                var level = headerMatch.Groups[1].Value.Length;
                var content = ProcessInlineElements(headerMatch.Groups[2].Value);
                return $"<h{level} data-line=\"{lineNumber}\">{content}</h{level}>\n";
            }

            var tableMatch = TableRowRegex.Match(trimmed);
            if (tableMatch.Success)
            {
                if (trimmed.Contains("---"))
                {
                    return string.Empty;
                }

                var cells = tableMatch.Groups[1].Value.Split('|');
                var rowHtml = new StringBuilder();
                foreach (var cell in cells)
                {
                    var cellTrimmed = cell.Trim();
                    if (string.IsNullOrEmpty(cellTrimmed)) continue;
                    rowHtml.Append($"<td>{ProcessInlineElements(cellTrimmed)}</td>");
                }
                return $"<tr data-line=\"{lineNumber}\">{rowHtml}</tr>\n";
            }

            if (!string.IsNullOrEmpty(trimmed))
            {
                var content = ProcessInlineElements(trimmed);
                return $"<p data-line=\"{lineNumber}\">{content}</p>\n";
            }

            return string.Empty;
        }

        private string ProcessInlineElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = EscapeHtml(text);

            text = InlineCodeRegex.Replace(text, "<code>$1</code>");
            text = LinkRegex.Replace(text, "<a href=\"$2\" target=\"_blank\">$1</a>");
            text = ImageRegex.Replace(text, "<img src=\"$2\" alt=\"$1\" />");
            text = text.Replace("***", "<strong><em>");
            text = text.Replace("**", "<strong>");
            text = text.Replace("*", "<em>");

            return text;
        }

        public string ConvertMarkdownToHtmlWithCharPositions(string markdown)
        {
            Console.WriteLine("ConvertMarkdownToHtmlWithCharPositions called");
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var lines = markdown.Split('\n');
            var result = new StringBuilder();
            bool inPre = false;
            string preLang = "";
            var preContent = new StringBuilder();
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var lineNumber = i + 1;
                var trimmed = line.Trim();

                if (CodeBlockRegex.IsMatch(trimmed))
                {
                    if (!inPre)
                    {
                        inPre = true;
                        var match = CodeBlockRegex.Match(trimmed);
                        preLang = match.Groups[1].Value;
                        preContent.Clear();
                        i++;
                        continue;
                    }
                    else
                    {
                        inPre = false;
                        result.Append($"<pre><code class=\"language-{EscapeHtml(preLang)}\" data-line=\"{lineNumber}\">{preContent.ToString()}</code></pre>\n");
                        preContent.Clear();
                        preLang = "";
                        i++;
                        continue;
                    }
                }

                if (inPre)
                {
                    var escapedLine = EscapeHtml(line);
                    var charSpan = new StringBuilder();
                    for (int j = 0; j < escapedLine.Length; j++)
                    {
                        charSpan.Append($"<span data-pos=\"{lineNumber}-{j + 1}\">{escapedLine[j]}</span>");
                    }
                    preContent.AppendLine(charSpan.ToString());
                    i++;
                    continue;
                }

                var listMatch = ListItemRegex.Match(trimmed);
                if (listMatch.Success)
                {
                    var tag = char.IsDigit(listMatch.Groups[2].Value[0]) ? "ol" : "ul";
                    int startLine = lineNumber;
                    result.Append($"<{tag}>\n");
                    
                    while (i < lines.Length)
                    {
                        line = lines[i];
                        lineNumber = i + 1;
                        trimmed = line.Trim();
                        
                        listMatch = ListItemRegex.Match(trimmed);
                        if (!listMatch.Success)
                            break;
                            
                        var content = ProcessInlineElementsWithPositions(listMatch.Groups[3].Value, lineNumber);
                        result.Append($"<li data-line=\"{lineNumber}\">{content}</li>\n");
                        i++;
                    }
                    
                    result.Append($"</{tag}>\n");
                    continue;
                }

                var html = ConvertLineToHtmlWithPositions(line, lineNumber);
                if (!string.IsNullOrEmpty(html))
                {
                    result.Append(html);
                }
                else
                {
                    result.Append($"<p data-line=\"{lineNumber}\">&nbsp;</p>\n");
                }

                i++;
            }

            if (inPre && preContent.Length > 0)
            {
                result.Append($"<pre><code class=\"language-{EscapeHtml(preLang)}\" data-line=\"{lines.Length}\">{preContent.ToString()}</code></pre>\n");
            }

            return result.ToString();
        }

        private string ConvertLineToHtmlWithPositions(string line, int lineNumber)
        {
            var trimmed = line.Trim();

            if (HrRegex.IsMatch(trimmed))
            {
                return $"<hr data-line=\"{lineNumber}\">\n";
            }

            var blockquoteMatch = BlockquoteRegex.Match(trimmed);
            if (blockquoteMatch.Success)
            {
                var content = ProcessInlineElementsWithPositions(blockquoteMatch.Groups[1].Value, lineNumber);
                return $"<blockquote data-line=\"{lineNumber}\">{content}</blockquote>\n";
            }

            var headerMatch = HeaderRegex.Match(trimmed);
            if (headerMatch.Success)
            {
                var level = headerMatch.Groups[1].Value.Length;
                var content = ProcessInlineElementsWithPositions(headerMatch.Groups[2].Value, lineNumber);
                return $"<h{level} data-line=\"{lineNumber}\">{content}</h{level}>\n";
            }

            var tableMatch = TableRowRegex.Match(trimmed);
            if (tableMatch.Success)
            {
                if (trimmed.Contains("---"))
                {
                    return string.Empty;
                }

                var cells = tableMatch.Groups[1].Value.Split('|');
                var rowHtml = new StringBuilder();
                foreach (var cell in cells)
                {
                    var cellTrimmed = cell.Trim();
                    if (string.IsNullOrEmpty(cellTrimmed)) continue;
                    rowHtml.Append($"<td>{ProcessInlineElementsWithPositions(cellTrimmed, lineNumber)}</td>");
                }
                return $"<tr data-line=\"{lineNumber}\">{rowHtml}</tr>\n";
            }

            if (!string.IsNullOrEmpty(trimmed))
            {
                var content = ProcessInlineElementsWithPositions(trimmed, lineNumber);
                return $"<p data-line=\"{lineNumber}\">{content}</p>\n";
            }

            return string.Empty;
        }

        private string ProcessInlineElementsWithPositions(string text, int lineNumber)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = EscapeHtml(text);
            
            var result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                result.Append($"<span data-pos=\"{lineNumber}-{i + 1}\" onclick=\"handleClick({lineNumber}, {i + 1});\">{text[i]}</span>");
            }
            return result.ToString();
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        public string ConvertToHtml(string markdown)
        {
            return ConvertMarkdownToHtml(markdown);
        }
    }
}