#r "src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/Markdig.dll"
#r "src/WeaveDoc.MarkdownEditor/bin/Debug/net10.0/WeaveDoc.MarkdownEditor.dll"

using System;
using System.IO;
using WeaveDoc.MarkdownEditor.Services;

var text = File.ReadAllText("tests/test_doc/markdown/test_latex.md");
var renderer = new MarkdigMarkdownRenderService();
try {
    var html = renderer.RenderAsync(text).GetAwaiter().GetResult();
    Console.WriteLine("Rendered HTML length: " + html.Length);
} catch (Exception ex) {
    Console.WriteLine("Error: " + ex);
}
