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
        private static readonly Regex TaskListItemRegex = new Regex(@"^(\s*)([-*])\s+\[([ xX])\]\s+(.+)$", RegexOptions.Multiline);
        private static readonly Regex BlockquoteRegex = new Regex(@"^>\s*(.+)$", RegexOptions.Multiline);
        private static readonly Regex CodeBlockRegex = new Regex(@"^```(\w*)$", RegexOptions.Multiline);
        private static readonly Regex BoldItalicRegex = new Regex(@"^\*\*\*(.+?)\*\*\*$");
        private static readonly Regex BoldRegex = new Regex(@"^\*\*(.+?)\*\*$");
        private static readonly Regex ItalicRegex = new Regex(@"^\*(.+?)\*$");
        private static readonly Regex StrikethroughRegex = new Regex(@"~~(.+?)~~");
        private static readonly Regex InlineCodeRegex = new Regex(@"`([^`]+)`");
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^\)]+)\)");
        private static readonly Regex ImageRegex = new Regex(@"!\[([^\]]*)\]\(([^\)]+)\)");
        private static readonly Regex HrRegex = new Regex(@"^---+$|^\*\*\*+$|^___+$", RegexOptions.Multiline);
        private static readonly Regex TableRowRegex = new Regex(@"^\|(.+)\|$");
        private static readonly Regex FootnoteRegex = new Regex(@"\[\^(\d+)\]:\s*(.+)$", RegexOptions.Multiline);
        private static readonly Regex FootnoteRefRegex = new Regex(@"\[\^(\d+)\]");

        // LaTeX 公式正则表达式
        // 行内公式：$...$（不包括 $$，避免与块级公式混淆）
        private static readonly Regex InlineMathRegex = new Regex(@"(?<!\$)\$(?!\$)([^\$]+)\$(?!\$)");
        // 块级公式：$$...$$
        private static readonly Regex DisplayMathRegex = new Regex(@"\$\$([\s\S]*?)\$\$");

        public string ConvertMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var lines = markdown.Split('\n');
            var result = new StringBuilder();
            bool inPre = false;
            string preLang = "";
            var preContent = new StringBuilder();
            bool inDisplayMath = false;
            var displayMathContent = new StringBuilder();
            int displayMathStartLine = 0;
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var lineNumber = i + 1;
                var trimmed = line.Trim();

                // 处理块级 LaTeX 公式（$$...$$）
                if (trimmed.StartsWith("$$") && !inPre && !inDisplayMath)
                {
                    if (trimmed == "$$")
                    {
                        // 开始新的块级公式
                        inDisplayMath = true;
                        displayMathStartLine = lineNumber;
                        displayMathContent.Clear();
                        i++;
                        continue;
                    }
                    else if (trimmed.EndsWith("$$") && trimmed.Length > 4)
                    {
                        // 单行块级公式 $$...$$
                        var mathContent = trimmed.Substring(2, trimmed.Length - 4);
                        // 块级公式内容保持原始格式，不转义HTML特殊字符
                        result.Append($"<div class=\"math-display\" data-line=\"{lineNumber}\">{mathContent}</div>\n");
                        i++;
                        continue;
                    }
                }

                if (inDisplayMath)
                {
                    if (trimmed.EndsWith("$$"))
                    {
                        // 结束块级公式
                        displayMathContent.AppendLine(trimmed.Substring(0, trimmed.Length - 2));
                        // 块级公式内容保持原始格式，不转义HTML特殊字符
                        result.Append($"<div class=\"math-display\" data-line=\"{displayMathStartLine}-{lineNumber}\">{displayMathContent.ToString().TrimEnd()}</div>\n");
                        inDisplayMath = false;
                        displayMathContent.Clear();
                    }
                    else
                    {
                        displayMathContent.AppendLine(line);
                    }
                    i++;
                    continue;
                }

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

            // 处理未闭合的块级 LaTeX 公式
            if (inDisplayMath && displayMathContent.Length > 0)
            {
                result.Append($"<div class=\"math-display\" data-line=\"{displayMathStartLine}-{lines.Length}\">{displayMathContent.ToString().TrimEnd()}</div>\n");
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

            // 第一步：提取所有行内 LaTeX 公式，用占位符替换（避免HTML转义破坏LaTeX语法）
            var mathPlaceholders = new List<string>();
            var processedText = InlineMathRegex.Replace(text, match =>
            {
                var mathContent = match.Value;
                int placeholderIndex = mathPlaceholders.Count;
                mathPlaceholders.Add(match.Value);
                return $"{{MATH_PH_{placeholderIndex}}}";
            });

            // 第二步：转义剩余的HTML特殊字符
            processedText = EscapeHtml(processedText);

            // 第三步：处理其他Markdown语法
            processedText = InlineCodeRegex.Replace(processedText, "<code>$1</code>");
            processedText = StrikethroughRegex.Replace(processedText, "<del>$1</del>");
            processedText = LinkRegex.Replace(processedText, "<a href=\"$2\" target=\"_blank\" rel=\"noopener noreferrer\">$1</a>");
            processedText = ImageRegex.Replace(processedText, "<img src=\"$2\" alt=\"$1\" />");
            processedText = FootnoteRefRegex.Replace(processedText, "<sup id=\"fnref:$1\"><a href=\"#fn:$1\" class=\"footnote-ref\">$1</a></sup>");

            processedText = processedText.Replace("***", "<strong><em>").Replace("**", "<strong>").Replace("*", "<em>");
            processedText = processedText.Replace("___", "<strong><em>").Replace("__", "<strong>").Replace("_", "<em>");

            // 第四步：还原LaTeX占位符为实际的span标签
            for (int i = 0; i < mathPlaceholders.Count; i++)
            {
                var mathContent = mathPlaceholders[i];
                // 提取 $...$ 中的内容
                var innerContent = mathContent.TrimStart('$').TrimEnd('$');
                processedText = processedText.Replace($"{{MATH_PH_{i}}}", $"<span class=\"math-inline\">{innerContent}</span>");
            }

            return processedText;
        }

        /// <summary>
        /// 处理行内 LaTeX 公式，将其转换为可被 KaTeX 渲染的格式
        /// 注意：LaTeX内容保持原始格式，不转义HTML特殊字符
        /// </summary>
        private string ProcessInlineMath(string text)
        {
            return InlineMathRegex.Replace(text, match =>
            {
                var mathContent = match.Groups[1].Value;
                return $"<span class=\"math-inline\">{mathContent}</span>";
            });
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
            bool inDisplayMath = false;
            var displayMathContent = new StringBuilder();
            int displayMathStartLine = 0;
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var lineNumber = i + 1;
                var trimmed = line.Trim();

                // 处理块级 LaTeX 公式（$$...$$）
                if (trimmed.StartsWith("$$") && !inPre && !inDisplayMath)
                {
                    if (trimmed == "$$")
                    {
                        // 开始新的块级公式
                        inDisplayMath = true;
                        displayMathStartLine = lineNumber;
                        displayMathContent.Clear();
                        i++;
                        continue;
                    }
                    else if (trimmed.EndsWith("$$") && trimmed.Length > 4)
                    {
                        // 单行块级公式 $$...$$
                        var mathContent = trimmed.Substring(2, trimmed.Length - 4);
                        // 块级公式内容保持原始格式，不转义HTML特殊字符
                        result.Append($"<div class=\"math-display\" data-line=\"{lineNumber}\">{mathContent}</div>\n");
                        i++;
                        continue;
                    }
                }

                if (inDisplayMath)
                {
                    if (trimmed.EndsWith("$$"))
                    {
                        // 结束块级公式
                        displayMathContent.AppendLine(trimmed.Substring(0, trimmed.Length - 2));
                        // 块级公式内容保持原始格式，不转义HTML特殊字符
                        result.Append($"<div class=\"math-display\" data-line=\"{displayMathStartLine}-{lineNumber}\">{displayMathContent.ToString().TrimEnd()}</div>\n");
                        inDisplayMath = false;
                        displayMathContent.Clear();
                    }
                    else
                    {
                        displayMathContent.AppendLine(line);
                    }
                    i++;
                    continue;
                }

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
                            
                        // 列表项格式: "  - content"，起始列 = 缩进 + 列表符号长度 + 空格
                        int indentLength = listMatch.Groups[1].Value.Length;
                        int markerLength = listMatch.Groups[2].Value.Length;
                        int startCol = indentLength + markerLength + 2;
                        var content = ProcessInlineElementsWithPositions(listMatch.Groups[3].Value, lineNumber, startCol);
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

            // 处理未闭合的块级 LaTeX 公式
            if (inDisplayMath && displayMathContent.Length > 0)
            {
                result.Append($"<div class=\"math-display\" data-line=\"{displayMathStartLine}-{lines.Length}\">{displayMathContent.ToString().TrimEnd()}</div>\n");
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
                // 块引用格式: "> content"，起始列 = 3（跳过 "> "）
                var content = ProcessInlineElementsWithPositions(blockquoteMatch.Groups[1].Value, lineNumber, 3);
                return $"<blockquote data-line=\"{lineNumber}\">{content}</blockquote>\n";
            }

            var headerMatch = HeaderRegex.Match(trimmed);
            if (headerMatch.Success)
            {
                var level = headerMatch.Groups[1].Value.Length;
                // 标题格式: "## content"，起始列 = level + 2（跳过 "#" 和空格）
                var content = ProcessInlineElementsWithPositions(headerMatch.Groups[2].Value, lineNumber, level + 2);
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
                int currentCol = 2; // 从 | 之后开始
                foreach (var cell in cells)
                {
                    var cellTrimmed = cell.Trim();
                    if (string.IsNullOrEmpty(cellTrimmed)) 
                    {
                        currentCol += cell.Length + 1;
                        continue;
                    }
                    // 找到实际内容的起始位置
                    int cellStart = line.IndexOf(cellTrimmed, currentCol - 1);
                    if (cellStart == -1) cellStart = currentCol;
                    rowHtml.Append($"<td>{ProcessInlineElementsWithPositions(cellTrimmed, lineNumber, cellStart + 1)}</td>");
                    currentCol = cellStart + cellTrimmed.Length + 1;
                }
                return $"<tr data-line=\"{lineNumber}\">{rowHtml}</tr>\n";
            }

            if (!string.IsNullOrEmpty(trimmed))
            {
                // 普通段落：找到第一个非空格字符的位置
                int firstNonSpace = line.IndexOf(trimmed[0]);
                if (firstNonSpace == -1) firstNonSpace = 0;
                var content = ProcessInlineElementsWithPositions(trimmed, lineNumber, firstNonSpace + 1);
                return $"<p data-line=\"{lineNumber}\">{content}</p>\n";
            }

            return string.Empty;
        }

        private string ProcessInlineElementsWithPositions(string text, int lineNumber, int startColumn = 1)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // 第一步：提取所有行内 LaTeX 公式，用占位符替换（避免HTML转义破坏LaTeX语法）
            var mathPlaceholders = new List<string>();
            var processedText = new StringBuilder();
            int i = 0;
            int currentColumn = startColumn;
            
            while (i < text.Length)
            {
                char currentChar = text[i];
                
                // 处理行内 LaTeX 公式 $...$
                if (currentChar == '$' && i + 1 < text.Length && text[i + 1] != '$')
                {
                    int startPos = currentColumn;
                    int endPos = -1;
                    for (int j = i + 1; j < text.Length; j++)
                    {
                        if (text[j] == '$' && (j + 1 >= text.Length || text[j + 1] != '$'))
                        {
                            endPos = j;
                            break;
                        }
                    }
                    
                    if (endPos > i)
                    {
                        // 找到完整的行内公式，保存到占位符列表
                        string mathContent = text.Substring(i + 1, endPos - i - 1);
                        int placeholderIndex = mathPlaceholders.Count;
                        mathPlaceholders.Add($"<span class=\"math-inline\" data-pos=\"{lineNumber}-{startPos}\">{mathContent}</span>");
                        
                        // 用占位符替换（LaTeX内容保持原始格式，不转义）
                        processedText.Append($"{{MATH_PH_{placeholderIndex}}}");
                        currentColumn += endPos - i + 1;
                        i = endPos + 1;
                        continue;
                    }
                }
                
                processedText.Append(currentChar);
                currentColumn++;
                i++;
            }
            
            // 第二步：处理转义字符和HTML特殊字符
            var escapedText = processedText.ToString();
            var result = new StringBuilder();
            currentColumn = startColumn;
            
            for (int j = 0; j < escapedText.Length; j++)
            {
                char c = escapedText[j];
                
                // 检查是否是LaTeX占位符
                if (c == '{' && escapedText.Substring(j).StartsWith("{MATH_PH_"))
                {
                    int endIndex = escapedText.IndexOf("}", j);
                    if (endIndex > j)
                    {
                        result.Append(escapedText.Substring(j, endIndex - j + 1));
                        j = endIndex;
                        continue;
                    }
                }
                
                // 处理转义字符
                if (c == '\\' && j + 1 < escapedText.Length)
                {
                    char nextChar = escapedText[j + 1];
                    string escapedChar;
                    
                    switch (nextChar)
                    {
                        case '\\': escapedChar = "\\"; break;
                        case '`': escapedChar = "`"; break;
                        case '*': escapedChar = "*"; break;
                        case '_': escapedChar = "_"; break;
                        case '{': escapedChar = "{"; break;
                        case '}': escapedChar = "}"; break;
                        case '[': escapedChar = "["; break;
                        case ']': escapedChar = "]"; break;
                        case '(': escapedChar = "("; break;
                        case ')': escapedChar = ")"; break;
                        case '#': escapedChar = "#"; break;
                        case '+': escapedChar = "+"; break;
                        case '-': escapedChar = "-"; break;
                        case '.': escapedChar = "."; break;
                        case '!': escapedChar = "!"; break;
                        case '<': escapedChar = "&lt;"; break;
                        case '>': escapedChar = "&gt;"; break;
                        case '&': escapedChar = "&amp;"; break;
                        case '$': escapedChar = "$"; break;  // 转义的 $ 不触发公式
                        default: 
                            escapedChar = "\\" + nextChar; 
                            break;
                    }
                    
                    result.Append($"<span data-pos=\"{lineNumber}-{currentColumn}\">{escapedChar}</span>");
                    currentColumn++;
                    j++; // 跳过已处理的下一个字符
                    continue;
                }
                
                // 处理HTML特殊字符
                string outputChar;
                switch (c)
                {
                    case '<':
                        outputChar = "&lt;";
                        break;
                    case '>':
                        outputChar = "&gt;";
                        break;
                    case '&':
                        outputChar = "&amp;";
                        break;
                    case '"':
                        outputChar = "&quot;";
                        break;
                    case '\'':
                        outputChar = "&#39;";
                        break;
                    default:
                        outputChar = c.ToString();
                        break;
                }
                
                result.Append($"<span data-pos=\"{lineNumber}-{currentColumn}\">{outputChar}</span>");
                currentColumn++;
            }
            
            // 第三步：还原LaTeX占位符为实际的HTML span
            string finalResult = result.ToString();
            for (int k = 0; k < mathPlaceholders.Count; k++)
            {
                finalResult = finalResult.Replace($"{{MATH_PH_{k}}}", mathPlaceholders[k]);
            }
            
            return finalResult;
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    case '\'':
                        sb.Append("&#39;");
                        break;
                    case '\\':
                        // 保留转义字符，让后续处理逻辑处理
                        sb.Append(c);
                        break;
                    default:
                        // 保留所有其他字符，包括 emoji 和 Unicode 字符
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public string ConvertToHtml(string markdown)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            
            return ConvertMarkdownToHtml(markdown);
        }
    }
}