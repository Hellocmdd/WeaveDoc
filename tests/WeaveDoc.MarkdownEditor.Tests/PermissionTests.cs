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
    public class PermissionTests
    {
        [AvaloniaTest]
        public async Task AppInit_WithNoPermissionFile_ShouldNotCrash()
        {
            // File.SetUnixFileMode 仅在 Unix 平台可用；Windows 上无法模拟“无权限”场景，跳过本测试。
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                Assert.Ignore("File.SetUnixFileMode requires a Unix platform; skipped on Windows.");
            }

            var tempFile = Path.GetTempFileName();
            File.SetUnixFileMode(tempFile, UnixFileMode.None); // No permissions

            try
            {
                var app = new App();
                var lifetime = new ClassicDesktopStyleApplicationLifetime
                {
                    Args = new[] { tempFile }
                };
                app.ApplicationLifetime = lifetime;

                Assert.DoesNotThrow(() => app.OnFrameworkInitializationCompleted());

                var mainWindow = lifetime.MainWindow as MainWindow;
                Assert.IsNotNull(mainWindow);

                // Now manually trigger OpenFileFromPathAsync because OnLoaded is not called in headless without show
                await mainWindow.OpenFileFromPathAsync(mainWindow.InitialFilePath);
            }
            finally
            {
                File.SetUnixFileMode(tempFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Delete(tempFile);
            }
        }
    }
}
