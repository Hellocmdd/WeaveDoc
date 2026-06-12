using System;
using System.Reflection;

namespace WeaveDoc.MarkdownEditor.Services
{
    class Program 
    {
        static void Main() 
        {
            var service = new MarkdownService();
            Console.WriteLine(service.ConvertMarkdownToHtml("Hello$x=1$World"));
        }
    }
}
