using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using WeaveDoc.App.Views;
using Xunit;

namespace WeaveDoc.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public async Task MainWindow_ContainsMarkdownEditorTab()
    {
        var window = new MainWindow();
        window.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var tabControl = window.FindControl<TabControl>("MainTabs");
            Assert.NotNull(tabControl);

            var headers = tabControl!.Items
                .OfType<TabItem>()
                .Select(item => item.Header?.ToString())
                .ToList();

            Assert.Contains("Markdown 编辑", headers);
        });
    }
}
