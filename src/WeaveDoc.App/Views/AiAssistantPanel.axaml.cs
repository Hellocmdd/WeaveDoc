using System.ComponentModel;
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

    /// <summary>
    /// Routes the <see cref="RagTabViewModel"/> (created by the shell when an AI service is present)
    /// to the three child views as their DataContext, and drives their visibility from code-behind.
    /// Visibility is managed here (not via XAML binding) because each child view's DataContext is the
    /// RagTabViewModel, so a <c>{Binding IsAiXxxTabSelected}</c> on the view itself would resolve against
    /// the wrong context and leave all three views stacked/overlapping.
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var shell = ViewModel;
        if (shell is null)
        {
            return;
        }

        var rag = shell.RagTabViewModel;
        if (rag is not null)
        {
            ChatView.DataContext = rag;
            CorpusView.DataContext = rag;
            SnapshotView.DataContext = rag;
        }

        var literature = shell.LiteratureViewModel;
        if (literature is not null)
        {
            LiteratureView.DataContext = literature;
        }

        shell.PropertyChanged -= OnShellPropertyChanged;
        shell.PropertyChanged += OnShellPropertyChanged;
        UpdateSubViewVisibility();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppShellViewModel.SelectedAiPanelTab))
        {
            UpdateSubViewVisibility();
        }
    }

    private void UpdateSubViewVisibility()
    {
        var shell = ViewModel;
        if (shell is null)
        {
            return;
        }

        ChatView.IsVisible = shell.IsAiChatTabSelected;
        LiteratureView.IsVisible = shell.IsAiLiteratureTabSelected;
        CorpusView.IsVisible = shell.IsAiCorpusTabSelected;
        SnapshotView.IsVisible = shell.IsAiSnapshotTabSelected;
    }

    private void OnSelectAiChatTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Chat);
    }

    private void OnSelectAiLiteratureTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Literature);
    }

    private void OnSelectAiCorpusTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Corpus);
    }

    private void OnSelectAiSnapshotTabClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectAiPanelTab(AiPanelTabKind.Snapshot);
    }
}
