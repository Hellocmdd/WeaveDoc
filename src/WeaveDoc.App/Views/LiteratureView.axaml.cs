using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class LiteratureView : UserControl
{
    public LiteratureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private LiteratureViewModel? ViewModel => DataContext as LiteratureViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ViewModel is { } vm)
        {
            vm.Entries.CollectionChanged -= OnEntriesChanged;
            vm.Entries.CollectionChanged += OnEntriesChanged;
        }
        UpdateEmptyState();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Entries.CollectionChanged -= OnEntriesChanged;
            vm.Entries.CollectionChanged += OnEntriesChanged;
        }
        UpdateEmptyState();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void UpdateEmptyState()
    {
        EmptyState.IsVisible = (ViewModel?.Entries.Count ?? 0) == 0;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            _ = vm.SearchAsync(SearchBox.Text ?? string.Empty);
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (vm is null || topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 BibTeX 文献库",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("BibTeX") { Patterns = ["*.bib"] },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        await vm.ImportBibAsync(files[0].Path.LocalPath);
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            await vm.RefreshAsync();
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LiteratureEntryViewModel entry } && ViewModel is { } vm)
        {
            await vm.DeleteAsync(entry.CitationKey);
        }
    }

    private void OnInsertCitationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LiteratureEntryViewModel entry } && ViewModel is { } vm)
        {
            vm.RequestInsertCitation(entry.CitationKey);
        }
    }
}
