using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(WeaveDoc.App.Tests.TestAppBuilder))]

namespace WeaveDoc.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .LogToTrace();
}

public class TestApp : Application
{
    public override void Initialize()
    {
        var fluent = new Avalonia.Themes.Fluent.FluentTheme();
        Styles.Add(fluent);
    }
}
