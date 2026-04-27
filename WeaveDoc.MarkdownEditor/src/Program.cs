using Avalonia;
using System;

namespace WeaveDoc.MarkdownEditor
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToTrace();
                      // 如果以后 Avalonia.ReactiveUI 发布 12.x，再加 .UseReactiveUI();
    }
}