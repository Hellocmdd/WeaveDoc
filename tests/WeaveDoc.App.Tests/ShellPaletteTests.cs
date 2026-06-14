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
        Assert.Equal(Color.FromRgb(0x5B, 0x8D, 0xEF), Brush("ShellAccentStrongBrush"));
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
        Assert.Equal(Color.FromRgb(0x9A, 0x6B, 0x3E), Brush("ShellAccentBrush"));
        Assert.Equal(Color.FromRgb(0x8A, 0x5C, 0x32), Brush("ShellAccentStrongBrush"));
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
