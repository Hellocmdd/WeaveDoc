using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class WorkspaceSidebar : UserControl
{
    public WorkspaceSidebar()
    {
        InitializeComponent();
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    private void OnSelectDocumentsTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectSidebarTab(WorkspaceSidebarTabKind.Documents);
    }

    private void OnSelectSettingsTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectSidebarTab(WorkspaceSidebarTabKind.Settings);
    }
}
