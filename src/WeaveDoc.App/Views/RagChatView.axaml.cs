using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class RagChatView : UserControl
{
    private bool _warmedUp;

    public RagChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private RagTabViewModel? ViewModel => DataContext as RagTabViewModel;

    /// <summary>Send on Enter, newline on Shift+Enter (matches the demo composer).</summary>
    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            SendOrStop();
        }
    }

    /// <summary>Pre-warm the (heavy) embedding/server stack the first time the user focuses the box,
    /// so the first answer isn't delayed by model load. Idempotent inside the service.</summary>
    private void OnPromptGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_warmedUp)
        {
            return;
        }

        _warmedUp = true;
        _ = ViewModel?.InitializeAsync();
    }

    private void OnSendClick(object? sender, RoutedEventArgs e) => SendOrStop();

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClearConversation();
    }

    private void SendOrStop()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        if (vm.IsBusy)
        {
            vm.StopGenerating();
        }
        else
        {
            _ = vm.SendAsync();
        }
    }

    /// <summary>Keep the conversation scrolled to the newest turn / streaming tail.</summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Turns.CollectionChanged -= OnTurnsChanged;
            vm.Turns.CollectionChanged += OnTurnsChanged;
            ScrollToEnd();
        }
    }

    private void OnTurnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEnd();

    private void ScrollToEnd()
    {
        // Run after layout so Extent reflects the newly added/replaced turn; ScrollViewer clamps the offset.
        Dispatcher.Post(() => ConversationScroll.Offset = new Vector(0, double.MaxValue),
            DispatcherPriority.Render);
    }
}
