using System;
using System.Reflection;
using Markdig;
using Markdig.Extensions.Mathematics;

class Program {
    static void Main() {
        var pipeline = new MarkdownPipelineBuilder().UseMathematics().Build();
        Console.WriteLine(Markdown.ToHtml("abc$a=b$def", pipeline));
        Console.WriteLine(Markdown.ToHtml("abc $a=b$ def", pipeline));
    }
}
