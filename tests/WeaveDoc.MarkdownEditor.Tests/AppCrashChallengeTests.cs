using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.NUnit;
using NUnit.Framework;
using WeaveDoc.MarkdownEditor;

namespace WeaveDoc.MarkdownEditor.Tests
{
    [TestFixture]
    public class AppCrashChallengeTests
    {
        [AvaloniaTest]
        public void AppInit_WithNullBytes_ShouldNotThrowArgumentException()
        {
            var app = new App();
            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                Args = new[] { "\0" }
            };
            app.ApplicationLifetime = lifetime;

            // The fix in App.axaml.cs catches the exception, so this should not throw.
            Assert.DoesNotThrow(() => app.OnFrameworkInitializationCompleted());
        }

        [AvaloniaTest]
        public void AppInit_WithInvalidCharacters_ShouldNotThrow()
        {
            var app = new App();
            var lifetime = new ClassicDesktopStyleApplicationLifetime
            {
                // This might not throw on Linux but wait, ArgumentException for \0 is sufficient to prove the flaw.
                Args = new[] { "\0" }
            };
            app.ApplicationLifetime = lifetime;
            Assert.DoesNotThrow(() => app.OnFrameworkInitializationCompleted());
        }
    }
}
