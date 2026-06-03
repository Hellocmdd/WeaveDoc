using System;
using WeaveDoc.MarkdownEditor.Services;

class TestLatexFix
{
    static void Main()
    {
        var service = new MarkdownService();
        
        string markdown = @"# 测试 LaTeX 环境

## 矩阵测试

$$
\begin{pmatrix}
a_{11} & a_{12} \\
a_{21} & a_{22}
\end{pmatrix}
$$

## 直接使用环境（不带 $$）

\begin{bmatrix}
1 & 2 & 3 \\
4 & 5 & 6 \\
7 & 8 & 9
\end{bmatrix}

## 方程组

\begin{cases}
x + y = 2 \\
2x - y = 1
\end{cases}

## 行内公式

这是行内矩阵 $\begin{pmatrix} 1 & 2 \\ 3 & 4 \end{pmatrix}$ 测试。
";
        
        Console.WriteLine("=== 原始 Markdown ===");
        Console.WriteLine(markdown);
        Console.WriteLine();
        
        Console.WriteLine("=== 转换后的 HTML ===");
        string html = service.ConvertMarkdownToHtml(markdown);
        Console.WriteLine(html);
        Console.WriteLine();
        
        Console.WriteLine("=== 验证结果 ===");
        Console.WriteLine($"包含 math-display: {html.Contains("math-display")}");
        Console.WriteLine($"包含 pmatrix: {html.Contains("pmatrix")}");
        Console.WriteLine($"包含 bmatrix: {html.Contains("bmatrix")}");
        Console.WriteLine($"包含 cases: {html.Contains("cases")}");
    }
}
