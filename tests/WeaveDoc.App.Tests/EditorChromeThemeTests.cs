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
/// The editor column is always-dark by design and is pinned to the Dark variant from
/// MainWindow.axaml (via ThemeVariantScope). The bug: Fluent's Button template renders the
/// auto-generated TextBlock inside a *disabled* button with a variant-dependent disabled brush.
/// Without the Dark-variant pin, the Light app theme makes that brush #66000000 (black @40%),
/// which is invisible on the dark editor panels — so the always-disabled editor-tab and the
/// no-document editor-tool buttons vanished in light theme even though the Button.Foreground
/// itself was set correctly. These tests lock in the fix.
/// </summary>
public class EditorChromeThemeTests
{
    [AvaloniaFact]
    public async Task EditorChrome_DisabledButtons_RenderLightText_InLightTheme()
    {
        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);

        // Dark -> Light. Raising Theme triggers OnShellPropertyChanged -> ApplyShellPalette(Light).
        await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());

        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        Assert.NotNull(editor);

        // The always-disabled editor-tab plus the (empty-state) disabled editor-tool buttons.
        var disabledButtons = editor!
            .GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("editor-tab") || b.Classes.Contains("editor-tool"))
            .Where(b => !b.IsEnabled)
            .ToList();
        Assert.NotEmpty(disabledButtons);

        foreach (var button in disabledButtons)
        {
            var inner = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            Assert.NotNull(inner);
            var color = Assert.IsType<SolidColorBrush>(inner!.Foreground).Color;
            // Under the pinned Dark variant the disabled brush resolves to white @40% (#66ffffff),
            // i.e. an effectively light colour. The buggy light-variant value is black @40% (#66000000).
            Assert.True(
                color.R > 200 && color.G > 200 && color.B > 200,
                $"disabled button '{button.Content}' inner TextBlock foreground {color} is dark and would be invisible on the editor panel in light theme.");
        }
    }

    [AvaloniaFact]
    public async Task EditorChrome_DirectTextStaysOnDark_InLightTheme()
    {
        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);
        await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());

        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        Assert.NotNull(editor);

        // The subtitle (CurrentDocumentSubtitle) is a direct TextBlock bound to the constant-light
        // ShellOnDarkMutedTextBrush — it must resolve to the light value (#8b949e), not flip dark.
        var subtitle = editor!
            .GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == "本地文档打开、保存和渲染能力待接入。");
        Assert.NotNull(subtitle);
        var color = Assert.IsType<SolidColorBrush>(subtitle!.Foreground).Color;
        Assert.Equal((byte)0x8b, color.R);
        Assert.Equal((byte)0x94, color.G);
        Assert.Equal((byte)0x9e, color.B);
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
            .First(b => b.Classes.Contains("editor-tab"));

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
