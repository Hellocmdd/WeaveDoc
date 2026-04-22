using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.Views;
using System;
using System.Threading.Tasks;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // 注册全局未处理异常处理器，帮助捕获运行时崩溃并写日志
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    if (e.ExceptionObject is Exception ex)
                        Logger.LogException(ex);
                    else
                        Logger.Log($"UnhandledException: {e.ExceptionObject}");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    Logger.LogException(e.Exception);
                }
                catch { }
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
        }
    }
}