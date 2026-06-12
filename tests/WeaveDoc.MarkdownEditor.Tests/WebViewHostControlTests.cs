using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Tests.Fakes;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class WebViewHostControlTests
    {
        [AvaloniaTest]
        public async Task PreviewWebViewControl_UpdatesHtmlContentThroughFakeHost()
        {
            var factory = new FakeWebViewHostFactory();
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Initial</h1>"
            };

            await control.Activate();
            var host = AssertSingleHost(factory);
            await WaitUntilAsync(() => host.InvokedScripts.Any(script => script.Contains("Initial", StringComparison.Ordinal)));

            Assert.That(host.NavigatedUris.Single().ToString(), Does.Contain("preview-template.html"));
            Assert.That(host.InvokedScripts.Last(script => script.Contains("Initial", StringComparison.Ordinal)), Does.Contain("window.updateContent"));
            Assert.That(control.IsUsingFallback, Is.False);

            control.HtmlContent = "<p>Changed</p>";
            await WaitUntilAsync(() => host.InvokedScripts.Any(script => script.Contains("Changed", StringComparison.Ordinal)));

            Assert.That(host.InvokedScripts.Last(script => script.Contains("Changed", StringComparison.Ordinal)), Does.Contain("window.updateContent"));
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_ShowsFallbackWhenHostFactoryFails()
        {
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = new ThrowingWebViewHostFactory("system WebKit missing")
            };

            await control.Activate();

            Assert.That(control.IsUsingFallback, Is.True);
            Assert.That(control.FallbackStatusText, Does.Contain("system WebKit missing"));
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_ShowsFallbackWhenNavigationNeverCompletes()
        {
            var factory = new FakeWebViewHostFactory
            {
                CompleteNavigation = false
            };
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                NavigationTimeout = TimeSpan.FromMilliseconds(10),
                HtmlContent = "<h1>Opened</h1>"
            };

            await control.Activate();
            await WaitUntilAsync(() => control.IsUsingFallback);

            Assert.That(control.FallbackStatusText, Does.Contain("导航超时"));
            Assert.That(control.HtmlContent, Is.EqualTo("<h1>Opened</h1>"));
            Assert.That(control.FallbackContentText, Is.EqualTo("Opened"));
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_UsesReadableFallbackWhenHostOnlySupportsNativeDialog()
        {
            var factory = new FakeWebViewHostFactory
            {
                AdapterDescription = "DetailedWebViewAdapterInfo { Type = WebKitGtk, SupportedScenarios = NativeDialog }"
            };
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1 data-line=\"1\"><span>Hello</span></h1><p>World&nbsp;again</p>"
            };

            await control.Activate();

            var host = AssertSingleHost(factory);
            var fallbackText = control.FindControl<TextBox>("PreviewFallbackContentTextBox");
            Assert.That(control.IsUsingFallback, Is.True);
            Assert.That(control.FallbackStatusText, Does.Contain("WebKitGTK"));
            Assert.That(control.FallbackContentText, Is.EqualTo($"Hello{Environment.NewLine}World again"));
            Assert.That(fallbackText?.Text, Is.EqualTo(control.FallbackContentText));
            Assert.That(host.InvokedScripts.Any(script => script.Contains("window.updateContent", StringComparison.Ordinal)), Is.False);
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_AllowsLinuxWebKitGtkOffscreenRenderer()
        {
            var factory = new FakeWebViewHostFactory
            {
                AdapterDescription = "DetailedWebViewAdapterInfo { Type = WebKitGtk, SupportedScenarios = OffscreenRenderer }"
            };
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Opened</h1>"
            };

            await control.Activate();
            var host = AssertSingleHost(factory);
            await WaitUntilAsync(() => host.InvokedScripts.Any(script => script.Contains("window.updateContent", StringComparison.Ordinal)));

            Assert.That(control.IsUsingFallback, Is.False);
            Assert.That(host.InvokedScripts.Last(script => script.Contains("window.updateContent", StringComparison.Ordinal)), Does.Contain("Opened"));
        }

        [AvaloniaTest]
        public async Task PdfViewerControl_LoadPdfAsync_SetsTargetPathAndNavigatesToPdfJs()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(filePath, [0x25, 0x50, 0x44, 0x46]);

            try
            {
                var factory = new FakeWebViewHostFactory();
                var control = new PdfViewerControl
                {
                    WebViewHostFactory = factory
                };

                await control.Activate();
                await control.LoadPdfAsync(filePath);

                var host = AssertSingleHost(factory);
                await WaitUntilAsync(() => host.InvokedScripts.Any(script => script.Contains("/pdf/current", StringComparison.Ordinal)));

                Assert.That(control.PdfFilePath, Is.EqualTo(filePath));
                Assert.That(host.NavigatedUris.Last().ToString(), Does.Contain("viewer.html?file=/pdf/current"));
                Assert.That(control.IsUsingFallback, Is.False);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [AvaloniaTest]
        public async Task PdfViewerControl_ShowsFallbackWhenNavigationNeverCompletes()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(filePath, [0x25, 0x50, 0x44, 0x46]);

            try
            {
                var factory = new FakeWebViewHostFactory
                {
                    CompleteNavigation = false
                };
                var control = new PdfViewerControl
                {
                    WebViewHostFactory = factory,
                    NavigationTimeout = TimeSpan.FromMilliseconds(10)
                };

                await control.Activate();
                await control.LoadPdfAsync(filePath);
                await WaitUntilAsync(() => control.IsUsingFallback);

                Assert.That(control.FallbackStatusText, Does.Contain("导航超时"));
                Assert.That(control.PdfFilePath, Is.EqualTo(filePath));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [AvaloniaTest]
        public async Task PdfViewerControl_UsesFallbackWhenHostOnlySupportsNativeDialog()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(filePath, [0x25, 0x50, 0x44, 0x46]);

            try
            {
                var factory = new FakeWebViewHostFactory
                {
                    AdapterDescription = "DetailedWebViewAdapterInfo { Type = WebKitGtk, SupportedScenarios = NativeDialog }"
                };
                var control = new PdfViewerControl
                {
                    WebViewHostFactory = factory
                };

                await control.Activate();
                await control.LoadPdfAsync(filePath);

                var host = AssertSingleHost(factory);
                var fallbackPath = control.FindControl<TextBox>("PdfFallbackFilePathTextBox");
                Assert.That(control.IsUsingFallback, Is.True);
                Assert.That(control.FallbackStatusText, Does.Contain("WebKitGTK"));
                Assert.That(control.PdfFilePath, Is.EqualTo(filePath));
                Assert.That(fallbackPath?.Text, Is.EqualTo(filePath));
                Assert.That(host.InvokedScripts.Any(script => script.Contains("PDFViewerApplication", StringComparison.Ordinal)), Is.False);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_ModeSwitching_DoesNotRecreateWebView()
        {
            var factory = new FakeWebViewHostFactory();
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Test</h1>"
            };

            await control.Activate();
            Assert.That(factory.Hosts, Has.Count.EqualTo(1));

            // 切换编辑/预览 10 次
            for (var i = 0; i < 10; i++)
            {
                control.Deactivate();
                await control.Activate();
            }

            // WebView 未被反复销毁重建
            Assert.That(factory.Hosts, Has.Count.EqualTo(1));
            Assert.That(control.IsUsingFallback, Is.False);
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_LinkClickedMessage_IsHandled()
        {
            var factory = new FakeWebViewHostFactory();
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Test</h1>"
            };

            await control.Activate();
            var host = AssertSingleHost(factory);

            // 模拟页面发送 linkClicked 消息
            host.SendMessage("""{"Type":"linkClicked","Data":"{\"url\":\"https://example.com\"}"}""");

            // 不抛异常，不进入 fallback
            Assert.That(control.IsUsingFallback, Is.False);
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_UnknownMessage_IsIgnored()
        {
            var factory = new FakeWebViewHostFactory();
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Test</h1>"
            };

            await control.Activate();
            var host = AssertSingleHost(factory);

            // 模拟页面发送未知类型消息
            host.SendMessage("""{"Type":"unknownMessageType","Data":"some data"}""");

            // 不抛异常，不进入 fallback
            Assert.That(control.IsUsingFallback, Is.False);
        }

        [AvaloniaTest]
        public async Task PreviewWebViewControl_PreviewLoadedMessage_IsHandled()
        {
            var factory = new FakeWebViewHostFactory();
            var control = new PreviewWebViewControl
            {
                WebViewHostFactory = factory,
                HtmlContent = "<h1>Test</h1>"
            };

            await control.Activate();
            var host = AssertSingleHost(factory);

            // 模拟页面发送 previewLoaded 消息
            host.SendMessage("""{"Type":"previewLoaded","Data":"loaded"}""");

            // 不抛异常，不进入 fallback
            Assert.That(control.IsUsingFallback, Is.False);
        }

        private static FakeWebViewHost AssertSingleHost(FakeWebViewHostFactory factory)
        {
            Assert.That(factory.Hosts, Has.Count.EqualTo(1));
            return factory.Hosts.Single();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (!condition())
            {
                if (cancellation.IsCancellationRequested)
                {
                    Assert.Fail("Timed out waiting for asynchronous WebView host interaction.");
                }

                await Task.Delay(10);
            }
        }
    }
}
