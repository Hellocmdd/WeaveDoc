#!/bin/bash
dotnet new console -n MathTest
cd MathTest
dotnet add reference ../src/WeaveDoc.MarkdownEditor/WeaveDoc.MarkdownEditor.csproj
cat << 'CSHARP' > Program.cs
using System;
using WeaveDoc.MarkdownEditor.Services;
class Program {
    static void Main() {
        var renderer = new MarkdigMarkdownRenderService();
        var html = renderer.RenderPreviewHtml("abc$x=1$def");
        Console.WriteLine(html);
    }
}
CSHARP
dotnet run
cd ..
rm -rf MathTest
