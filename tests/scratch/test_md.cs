using System;
using System.Reflection;
using WeaveDoc.MarkdownEditor.Services;

class Program {
    static void Main() {
        var s = new MarkdownService();
        Console.WriteLine(s.ConvertMarkdownToHtml("Hello$x=1$World"));
        Console.WriteLine(s.ConvertMarkdownToHtml("Hello $x=1$ World"));
    }
}
