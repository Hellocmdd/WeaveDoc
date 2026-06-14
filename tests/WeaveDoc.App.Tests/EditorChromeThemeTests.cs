using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.App.ViewModels;
using WeaveDoc.App.Views;
using Xunit;

namespace WeaveDoc.App.Tests;

/// <summary>
/// Regression coverage for the light-theme editor-chrome invisibility bug.
///
/// The editor column now follows the shell theme instead of pinning itself to an
/// always-dark variant. These tests make sure disabled toolbar text and direct
/// editor chrome text resolve to the light-theme shell brushes.
/// </summary>
public class EditorChromeThemeTests
{
    [AvaloniaFact]
    public async Task EditorChrome_DisabledButtons_RenderReadableText_InLightTheme()
    {
        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);

        // Dark -> Light. Raising Theme triggers OnShellPropertyChanged -> ApplyShellPalette(Light).
        await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());

        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        Assert.NotNull(editor);

        var disabledButtons = editor!
            .GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("icon-button"))
            .Where(b => !b.IsEnabled)
            .ToList();
        Assert.NotEmpty(disabledButtons);

        foreach (var button in disabledButtons)
        {
            var inner = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            Assert.NotNull(inner);
            var color = Assert.IsType<SolidColorBrush>(inner!.Foreground).Color;
            Assert.True(
                color.R < 190 && color.G < 190 && color.B < 190,
                $"disabled button '{button.Content}' inner TextBlock foreground {color} is too light for the light editor chrome.");
        }
    }

    [AvaloniaFact]
    public async Task EditorChrome_DirectTextFollowsLightTheme_InLightTheme()
    {
        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);
        await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());

        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        Assert.NotNull(editor);

        var subtitle = editor!
            .GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == "本地文档打开、保存和渲染能力待接入。");
        Assert.NotNull(subtitle);
        var color = Assert.IsType<SolidColorBrush>(subtitle!.Foreground).Color;
        Assert.Equal((byte)0x57, color.R);
        Assert.Equal((byte)0x60, color.G);
        Assert.Equal((byte)0x6a, color.B);
    }

    [AvaloniaFact]
    public async Task EditorTab_HidesWhenDocumentOpens()
    {
        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);
        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        Assert.NotNull(editor);

        Button FindTab() => editor!
            .GetVisualDescendants().OfType<Button>()
            .First(b => b.Classes.Contains("panel-tab") && b.Content?.ToString() == "未打开文档");

        // No document open: the placeholder tab is visible ("未打开文档").
        Assert.True(vm.HasNoDocument);
        await Dispatcher.UIThread.InvokeAsync(() => Assert.True(FindTab().IsVisible));

        // Open a real markdown document.
        var tmp = Path.Combine(Path.GetTempPath(), $"weavedoc-tab-{System.Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tmp, "# hello");
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await vm.DocumentWorkspace.OpenAsync(tmp));
            Assert.False(vm.HasNoDocument);
            await Dispatcher.UIThread.InvokeAsync(() => Assert.False(FindTab().IsVisible));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
