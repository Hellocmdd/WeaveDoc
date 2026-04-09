# LlamaSharp + Avalonia Demo

这是一个最小 Avalonia 桌面应用示例，展示如何在 UI 中集成 LlamaSharp（已在项目中引用）。

功能说明：
- 可输入提示文本并点击 `Run`。
- 可选填写模型路径并勾选 `Use LlamaSharp`，如果模型文件存在则会提示找到模型，但示例中不会实际加载模型（以避免运行时依赖模型权重）。
- 当未提供模型文件或未勾选时，应用使用本地模拟生成，保证可运行。

快速运行：
```bash
dotnet build
dotnet run
```

如果你有 LlamaSharp 支持的模型文件并希望我把示例改为真实调用，请告诉我模型格式和加载方式（或授权我查阅 LlamaSharp 的使用文档），我会把调用逻辑补上。
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
