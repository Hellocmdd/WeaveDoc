using Avalonia.Controls;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class WorkspaceSidebar : UserControl
{
    public WorkspaceSidebar()
    {
        InitializeComponent();
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;
}
