using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using NUnit.Framework;
using Avalonia.Headless.NUnit;
using WeaveDoc.MarkdownEditor.Controls.Web;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class NativeWebViewStressTest
    {
        [AvaloniaTest]
        public async Task TestViewportSizeAndOverlays()
        {
            var host = new NativeWebViewHost();
            var window = new Window
            {
                Width = 800,
                Height = 600,
                Content = host.View
            };
            window.Show();

            // Wait for adapter
            await Task.Delay(2000);

            // Navigate
            host.NavigateToString("<html><body><div id='test'>Ready</div></body></html>", new Uri("http://localhost"));
            await Task.Delay(2000);

            string width = await host.InvokeScriptAsync("window.innerWidth.toString()");
            string height = await host.InvokeScriptAsync("window.innerHeight.toString()");

            Console.WriteLine($"Viewport Size: {width} x {height}");

            window.Close();
        }
    }
}
