using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using WeaveDoc.MarkdownEditor.Controls.Web;

namespace Harness
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }

    public class App : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;
                
                // Run tests automatically
                Task.Run(async () => {
                    await Task.Delay(2000);
                    await window.RunTests();
                    Environment.Exit(0);
                });
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    public class MainWindow : Window
    {
        public NativeWebViewHost Host { get; }
        public Button OverlayButton { get; }

        public MainWindow()
        {
            Width = 800;
            Height = 600;

            Host = new NativeWebViewHost();
            OverlayButton = new Button 
            { 
                Content = "Overlay Button", 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                ZIndex = 100
            };

            var grid = new Grid();
            grid.Children.Add(Host.View);
            grid.Children.Add(OverlayButton);

            Content = grid;
        }

        public async Task RunTests()
        {
            try
            {
                Host.NavigateToString("<html><body style='margin:0;padding:0;'><div id='box' style='width:100vw;height:100vh;background:red;'></div></body></html>", new Uri("http://localhost/"));
                await Task.Delay(2000);
                
                string w = await Host.InvokeScriptAsync("window.innerWidth.toString()");
                string h = await Host.InvokeScriptAsync("window.innerHeight.toString()");
                Console.WriteLine($"[TEST_RESULT] Viewport: {w} x {h}");

                // We also want to know if the native window is hiding our Avalonia overlay.
                // In a headless or Wayland environment without xvfb, it might crash.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST_ERROR] {ex}");
            }
        }
    }
}
