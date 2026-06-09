using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.Views;

namespace WeaveDoc.MarkdownEditor
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                if (desktop.Args != null && desktop.Args.Length > 0 && !string.IsNullOrWhiteSpace(desktop.Args[0]))
                {
                    try
                    {
                        mainWindow.InitialFilePath = System.IO.Path.GetFullPath(desktop.Args[0]);
                    }
                    catch (System.ArgumentException)
                    {
                        // Ignore invalid path arguments
                    }
                    catch (System.NotSupportedException)
                    {
                        // Ignore paths with invalid formats (e.g., colons in wrong places)
                    }
                    catch (System.IO.PathTooLongException)
                    {
                        // Ignore excessively long paths
                    }
                    catch (System.Security.SecurityException)
                    {
                        // Ignore paths we don't have permission to access
                    }
                }
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}