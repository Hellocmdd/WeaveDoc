using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using LLama;
using LLama.Common;

namespace TestApp
{
	class Program
	{
		[STAThread]
		public static int Main(string[] args)
		{
			// Quick CLI test: dotnet run -- --test-load <modelpath>
			if (args.Length >= 2 && args[0] == "--test-load")
			{
				var modelPath = args[1];
				try
				{
					Console.WriteLine($"Testing load of model: {modelPath}");
					var parameters = new ModelParams(modelPath) { ContextSize = 2048 };
					using var w = LLamaWeights.LoadFromFile(parameters);
					Console.WriteLine("Model loaded successfully.");
					return 0;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Model load failed:\n" + ex.ToString());
					return 2;
				}
			}

			BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			return 0;
		}

		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace();
	}
}
