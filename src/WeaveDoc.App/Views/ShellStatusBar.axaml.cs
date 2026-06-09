using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class ShellStatusBar : UserControl
{
    public ShellStatusBar()
    {
        InitializeComponent();
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleTheme();
    }
}
