using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using NUnit.Framework;
using Avalonia.Headless.NUnit;
using WeaveDoc.MarkdownEditor;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class AppInitTests
    {
        [AvaloniaTest]
        public void App_Init_WithInvalidPath_ShouldNotCrash()
        {
            var app = new App();
            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                Args = new[] { "\0" }
            };
            app.ApplicationLifetime = lifetime;

            // This will throw ArgumentException
            app.OnFrameworkInitializationCompleted();
        }

        [AvaloniaTest]
        public void App_InitializesAvaloniaEditFluentTheme()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var appAxaml = File.ReadAllText(Path.Combine(repoRoot, "src/WeaveDoc.MarkdownEditor/App.axaml"));

            Assert.That(appAxaml, Does.Contain("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"));
        }
    }
}
