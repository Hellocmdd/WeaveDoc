using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using WeaveDoc.MarkdownEditor.Controls.Web;
using WeaveDoc.MarkdownEditor.Tests.Fakes;

[assembly: AvaloniaTestApplication(typeof(WeaveDoc.MarkdownEditor.Tests.TestAppBuilder))]

namespace WeaveDoc.MarkdownEditor.Tests
{
    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            WebViewHostFactoryProvider.Current = new FakeWebViewHostFactory();

            return AppBuilder.Configure<TestApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .LogToTrace();
        }
    }

    public sealed class TestApp : App
    {
    }
}
