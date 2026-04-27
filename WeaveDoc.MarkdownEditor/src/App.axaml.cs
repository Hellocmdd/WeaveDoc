using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.Views;
using System;
using System.Threading.Tasks;
using WeaveDoc.MarkdownEditor.Helpers;
using Microsoft.Web.WebView2.Core;

namespace WeaveDoc.MarkdownEditor
{
    public partial class App : Application
    {
        public static CoreWebView2Environment? WebView2Environment { get; private set; }

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

        public override async void OnFrameworkInitializationCompleted()
        {
            Logger.Log("App: Starting OnFrameworkInitializationCompleted...");
            
            // 提前创建 WebView2 环境，避免线程模式冲突
            try
            {
                WebView2Environment = await CoreWebView2Environment.CreateAsync();
                Logger.Log("WebView2 environment created successfully");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Logger.Log("App: Creating MainWindow...");
                desktop.MainWindow = new MainWindow();
                Logger.Log("App: MainWindow created successfully");
            }
            else
            {
                Logger.Log("App: ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime");
            }

            Logger.Log("App: Calling base.OnFrameworkInitializationCompleted()...");
            base.OnFrameworkInitializationCompleted();
            Logger.Log("App: OnFrameworkInitializationCompleted completed");
        }
    }
}