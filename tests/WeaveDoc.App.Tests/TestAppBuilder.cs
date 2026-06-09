using Avalonia;
using Avalonia.Headless;
using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.MarkdownEditor.Controls.Web;

[assembly: AvaloniaTestApplication(typeof(WeaveDoc.App.Tests.TestAppBuilder))]

namespace WeaveDoc.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        WebViewHostFactoryProvider.Current = new FakeWebViewHostFactory();

        return AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .LogToTrace();
    }
}

public class TestApp : WeaveDoc.App.App
{
    public override void OnFrameworkInitializationCompleted()
    {
    }
}
