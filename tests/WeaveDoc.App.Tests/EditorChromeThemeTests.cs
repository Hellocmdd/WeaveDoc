using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.App.Tests.Fakes;
using WeaveDoc.App.ViewModels;
using WeaveDoc.App.Views;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Controls.Web;
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
        Assert.Equal((byte)0x96, color.R);
        Assert.Equal((byte)0x8D, color.G);
        Assert.Equal((byte)0x7B, color.B);
    }

    [AvaloniaFact]
    public async Task PreviewWebView_ViewerCssTheme_BindsToShellTheme()
    {
        var window = new MainWindow();
        window.Show();
        var editor = window.FindControl<EditorWorkspace>("EditorWorkspaceControl");
        var preview = editor!.FindControl<PreviewWebViewControl>("MarkdownPreviewControl");
        Assert.NotNull(preview);

        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);
        Assert.Equal(ShellThemeKind.Dark, vm.Theme);
        await Dispatcher.UIThread.InvokeAsync(() => { });

        var atDark = preview!.ViewerCssTheme;
        await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());
        var atLight = preview.ViewerCssTheme;

        // Whatever the enum->string binding produces, it MUST normalize to dark/light
        // (not stay "Auto"), otherwise the preview HTML follows the OS theme instead
        // of the app theme.
        Assert.True(
            atDark.ToLowerInvariant() == "dark" || atDark.ToLowerInvariant() == "light",
            $"dark shell: ViewerCssTheme='{atDark}' (expected dark/light, not Auto)");
        Assert.True(
            atLight.ToLowerInvariant() == "dark" || atLight.ToLowerInvariant() == "light",
            $"light shell: ViewerCssTheme='{atLight}' (expected dark/light, not Auto)");
        Assert.NotEqual(atDark.ToLowerInvariant(), atLight.ToLowerInvariant());
    }

    [AvaloniaFact]
    public async Task PreviewWebView_ThemeScript_RunsOnThemeToggle()
    {
        // Spy diagnostic: does ApplyPreviewThemeAsync actually call InvokeScriptAsync
        // (with the dataset.weavedocTheme payload) when the shell theme flips?
        var factory = (FakeWebViewHostFactory)WebViewHostFactoryProvider.Current;
        var hostsAtStart = factory.Hosts.Count;

        var window = new MainWindow();
        window.Show();
        var vm = Assert.IsType<AppShellViewModel>(window.DataContext);

        // Open a document so IsMarkdownPreviewVisible can become true, then switch
        // to preview mode to force PreviewWebViewControl.Activate -> WebView init.
        var tmp = Path.Combine(Path.GetTempPath(), $"preview-theme-{System.Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tmp, "# hello");
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await vm.DocumentWorkspace.OpenAsync(tmp));
            await Dispatcher.UIThread.InvokeAsync(() => vm.SelectEditorMode(EditorSurfaceMode.Preview));

            // Let activate + navigation + NavigationCompleted (Fake fires it synchronously
            // off NavigateToString) + the follow-up ApplyPreviewThemeAsync settle.
            await Task.Delay(2500);

            var newHosts = factory.Hosts.Skip(hostsAtStart).ToList();
            Assert.NotEmpty(newHosts);
            var host = newHosts[^1];
            Assert.NotEmpty(host.InvokedScripts); // navigation ran at least the content script

            var baseline = host.InvokedScripts.Count;
            await Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());
            await Task.Delay(800);

            var afterToggle = host.InvokedScripts.Skip(baseline)
                .Where(s => s.Contains("weavedocTheme", System.StringComparison.Ordinal))
                .ToList();
            Assert.NotEmpty(afterToggle);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
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
