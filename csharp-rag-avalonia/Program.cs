using Avalonia;
using RagAvalonia.Services;

namespace RagAvalonia;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        if (TryGetEvalFilePath(args, out var evalFilePath))
        {
            var exitCode = await EvalRunner.RunAsync(evalFilePath);
            Environment.ExitCode = exitCode;
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
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
