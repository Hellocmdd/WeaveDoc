using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class AiAssistantPanel : UserControl
{
    public AiAssistantPanel()
    {
        InitializeComponent();
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    private void OnToggleAiPanelClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleAiPanel();
    }

    private void OnSelectAiChatTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Chat);
    }

    private void OnSelectAiLiteratureTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Literature);
    }

    private void OnSelectAiSnapshotTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Snapshot);
    }
}
