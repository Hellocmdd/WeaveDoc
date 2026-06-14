using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using WeaveDoc.App.ViewModels;
using WeaveDoc.App.Views;
using Xunit;

namespace WeaveDoc.App.Tests;

public class ShellPaletteTests
{
    [AvaloniaFact]
    public void DarkPalette_UsesModernDarkDirectionB()
    {
        var window = new MainWindow();
        window.Show();

        Assert.Equal(ShellThemeKind.Dark, ((AppShellViewModel)window.DataContext!).Theme);

        Assert.Equal(Color.FromRgb(0x0B, 0x0E, 0x14), Brush("ShellBackgroundBrush"));
        Assert.Equal(Color.FromRgb(0x11, 0x15, 0x1E), Brush("ShellChromeBrush"));
        Assert.Equal(Color.FromRgb(0x0F, 0x13, 0x1B), Brush("ShellPanelBrush"));
        Assert.Equal(Color.FromRgb(0x17, 0x1C, 0x26), Brush("ShellCardBrush"));
        Assert.Equal(Color.FromRgb(0x21, 0x28, 0x36), Brush("ShellBorderBrush"));
        Assert.Equal(Color.FromRgb(0x7C, 0x9C, 0xFF), Brush("ShellAccentBrush"));
        Assert.Equal(Color.FromRgb(0x6E, 0x92, 0xFF), Brush("ShellAccentStrongBrush"));
        Assert.Equal(Color.FromRgb(0x1B, 0x25, 0x40), Brush("ShellAccentHoverBrush"));
        // Ghost is fully transparent but RGB-matched to the hover tint so the
        // leave-hover brush transition animates alpha only (no grey flash).
        Assert.Equal(Color.FromArgb(0x00, 0x1B, 0x25, 0x40), Brush("ShellAccentHoverGhostBrush"));
        Assert.Equal(Color.FromRgb(0x82, 0xA4, 0xFF), Brush("ShellAccentHoverStrongBrush"));
        Assert.Equal(Color.FromRgb(0xE6, 0xED, 0xF3), Brush("ShellTextBrush"));
        Assert.Equal(Color.FromRgb(0x8B, 0x95, 0xA7), Brush("ShellMutedTextBrush"));
    }

    [AvaloniaFact]
    public async Task LightPalette_UsesWarmPaperDirectionC()
    {
        var window = new MainWindow();
        window.Show();
        var vm = (AppShellViewModel)window.DataContext!;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => vm.ToggleTheme());

        Assert.Equal(Color.FromRgb(0xFA, 0xF8, 0xF3), Brush("ShellBackgroundBrush"));
        Assert.Equal(Color.FromRgb(0xF1, 0xED, 0xE4), Brush("ShellChromeBrush"));
        Assert.Equal(Color.FromRgb(0xF6, 0xF3, 0xEC), Brush("ShellPanelBrush"));
        Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0xFF), Brush("ShellCardBrush"));
        Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0xFF), Brush("ShellEditorBackgroundBrush"));
        Assert.Equal(Color.FromRgb(0xE2, 0xDC, 0xD0), Brush("ShellBorderBrush"));
        Assert.Equal(Color.FromRgb(0xA8, 0x66, 0x2C), Brush("ShellAccentBrush"));
        Assert.Equal(Color.FromRgb(0x8A, 0x53, 0x28), Brush("ShellAccentStrongBrush"));
        Assert.Equal(Color.FromRgb(0xF0, 0xE2, 0xCB), Brush("ShellAccentHoverBrush"));
        Assert.Equal(Color.FromArgb(0x00, 0xF0, 0xE2, 0xCB), Brush("ShellAccentHoverGhostBrush"));
        Assert.Equal(Color.FromRgb(0x9C, 0x62, 0x33), Brush("ShellAccentHoverStrongBrush"));
        Assert.Equal(Color.FromRgb(0x32, 0x2E, 0x26), Brush("ShellTextBrush"));
        Assert.Equal(Color.FromRgb(0x96, 0x8D, 0x7B), Brush("ShellMutedTextBrush"));
        Assert.Equal(Color.FromRgb(0xB5, 0xAB, 0x97), Brush("ShellDisabledTextBrush"));
    }

    [AvaloniaFact]
    public void FormTokens_AndPrimaryGlow_AreRegistered()
    {
        var window = new MainWindow();
        window.Show();
        var res = Avalonia.Application.Current!.Resources;

        Assert.NotNull(res["ShellRadiusSm"]);
        Assert.NotNull(res["ShellRadiusMd"]);
        Assert.NotNull(res["ShellRadiusLg"]);
        Assert.NotNull(res["ShellPrimaryGlowDark"]);
        Assert.NotNull(res["ShellPrimaryGlowLight"]);

        // Dark active by default -> current glow is the dark one.
        Assert.Same(res["ShellPrimaryGlowDark"], res["ShellPrimaryGlow"]);
    }

    [AvaloniaFact]
    public void CommandButton_UsesTokenRadius_AndPrimaryGlow()
    {
        var window = new MainWindow();
        window.Show();

        var btn = window.FindControl<Avalonia.Controls.Button>("ExportShellDocumentButton");
        Assert.NotNull(btn);
        Assert.Contains("primary", btn!.Classes);
        Assert.Equal(7, btn.CornerRadius.TopLeft);

        var glow = Avalonia.Application.Current!.Resources["ShellPrimaryGlow"];
        Assert.NotNull(glow);
    }

    [AvaloniaFact]
    public void Buttons_HaveSmoothBrushTransitions()
    {
        // Pointer-over used to be an instantaneous flash. The global Button style
        // now installs BrushTransitions so Background/BorderBrush/Foreground ease.
        var window = new MainWindow();
        window.Show();

        var btn = window.FindControl<Avalonia.Controls.Button>("ExportShellDocumentButton");
        Assert.NotNull(btn);
        // Global Button style installs Background/BorderBrush/Foreground brush transitions.
        Assert.NotNull(btn!.Transitions);
        Assert.True(btn.Transitions!.Count >= 3);
    }

    [AvaloniaFact]
    public void IconGeometries_AndShellIconStyle_AreRegistered()
    {
        var window = new MainWindow();
        window.Show();
        var res = Avalonia.Application.Current!.Resources;

        foreach (var key in new[] { "IconNew", "IconOpen", "IconSave", "IconSettings",
                                    "IconExport", "IconChat", "IconLiterature", "IconSnapshot",
                                    "IconSearch", "IconSun", "IconMoon" })
        {
            Assert.NotNull(res[key]);
        }
    }

    private static Color Brush(string key)
    {
        var brush = Assert.IsType<SolidColorBrush>(Avalonia.Application.Current!.Resources[key]);
        return brush.Color;
    }
}
