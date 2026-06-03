using System;
using WeaveDoc.MarkdownEditor.Services;

class TestLatex
{
    static void Main()
    {
        var service = new MarkdownService();
        
        string markdown = @"# 测试LaTeX

这是一个简单测试：$x + y = z$

这是分数：$\frac{a}{b}$

这是复杂公式：$\int_{0}^{1} x^2 dx$

块级公式：
$$
\sum_{i=1}^{n} i = \frac{n(n+1)}{2}
$$
";
        
        Console.WriteLine("=== 原始 Markdown ===");
        Console.WriteLine(markdown);
        Console.WriteLine();
        
        Console.WriteLine("=== 转换后的 HTML ===");
        string html = service.ConvertMarkdownToHtmlWithCharPositions(markdown);
        Console.WriteLine(html);
        Console.WriteLine();
        
        Console.WriteLine("=== 检查 math-inline 和 math-display ===");
        Console.WriteLine($"包含 math-inline: {html.Contains("math-inline")}");
        Console.WriteLine($"包含 math-display: {html.Contains("math-display")}");
    }
}
