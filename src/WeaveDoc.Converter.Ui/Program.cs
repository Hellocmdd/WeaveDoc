using System;
using System.IO;
using Avalonia;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;

namespace WeaveDoc.Converter.Ui;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "weavedoc.db");
        var configManager = new ConfigManager(dbPath);
        configManager.EnsureSeedTemplatesAsync().GetAwaiter().GetResult();

        var pandoc = new PandocPipeline();
        var engine = new DocumentConversionEngine(pandoc, configManager);

        BuildAvaloniaApp(configManager, engine)
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp(
        ConfigManager configManager,
        DocumentConversionEngine engine)
        => AppBuilder.Configure(() => new App(configManager, engine))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
