using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class EdgeCaseTests
    {
        [AvaloniaTest]
        public void AppInit_WithNonExistentFile_ShouldNotCrash()
        {
            var app = new App();
            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                Args = new[] { "/non/existent/file/path/that/does/not/exist.md" }
            };
            app.ApplicationLifetime = lifetime;

            Assert.DoesNotThrow(() => app.OnFrameworkInitializationCompleted());

            var mainWindow = lifetime.MainWindow as MainWindow;
            Assert.IsNotNull(mainWindow);
            Assert.AreEqual(Path.GetFullPath("/non/existent/file/path/that/does/not/exist.md"), mainWindow.InitialFilePath);
        }

        [AvaloniaTest]
        public void AppInit_WithDirectoryPath_ShouldNotCrash()
        {
            var app = new App();
            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                Args = new[] { "/" }
            };
            app.ApplicationLifetime = lifetime;

            Assert.DoesNotThrow(() => app.OnFrameworkInitializationCompleted());
        }
    }
}
