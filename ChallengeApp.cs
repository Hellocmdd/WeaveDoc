using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using WeaveDoc.MarkdownEditor;

namespace ChallengeApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();
            var lifetime = new MyLifetime { Args = new string[] { args[0] } };
            app.ApplicationLifetime = lifetime;
            app.OnFrameworkInitializationCompleted();
            Console.WriteLine("Done");
        }
    }

    class MyLifetime : IClassicDesktopStyleApplicationLifetime
    {
        public string[] Args { get; set; }
        public Avalonia.Controls.Window MainWindow { get; set; }
        public event EventHandler<Avalonia.Controls.ApplicationLifetimes.ControlledApplicationLifetimeStartupEventArgs> Startup;
        public event EventHandler<Avalonia.Controls.ApplicationLifetimes.ControlledApplicationLifetimeExitEventArgs> Exit;
        public Avalonia.Controls.ShutdownMode ShutdownMode { get; set; }
        public void Shutdown(int exitCode = 0) {}
        public bool TryShutdown(int exitCode = 0) => true;
    }
}
