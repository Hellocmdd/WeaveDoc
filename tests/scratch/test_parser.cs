using System;
using Markdig;
using Markdig.Extensions.Mathematics;

class Program {
    static void Main() {
        var builder = new MarkdownPipelineBuilder().UseMathematics();
        var defaultParser = builder.InlineParsers.FindExact<MathInlineParser>();
        Console.WriteLine($"Found default parser: {defaultParser != null}");
    }
}
