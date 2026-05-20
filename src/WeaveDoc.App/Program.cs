using Avalonia;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;
using WeaveDoc.Converter.Pandoc;
using WeaveDoc.Rag.Services;

namespace WeaveDoc.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TryGetEvalFilePath(args, out var evalFilePath))
        {
            var exitCode = EvalRunner.RunAsync(evalFilePath).GetAwaiter().GetResult();
            Environment.ExitCode = exitCode;
            return;
        }

        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
            "Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCekx0Rnxbf1x2ZFFMY15bRXFPMyBoS35RcEVnWHledHdXR2dYVkZyVEFe");

        var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "weavedoc.db");
        var configManager = new ConfigManager(dbPath);
        configManager.EnsureSeedTemplatesAsync().GetAwaiter().GetResult();

        var pandoc = new PandocPipeline();
        var pdfConverter = new SyncfusionPdfConverter();
        var engine = new DocumentConversionEngine(pandoc, pdfConverter, configManager);

        BuildAvaloniaApp(configManager, engine)
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(
        ConfigManager configManager,
        DocumentConversionEngine engine)
    {
        return AppBuilder.Configure(() => new App(configManager, engine))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static bool TryGetEvalFilePath(string[] args, out string evalFilePath)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--eval", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new ArgumentException("Missing value for --eval.");
            }

            evalFilePath = args[index + 1];
            return true;
        }

        evalFilePath = string.Empty;
        return false;
    }
}
