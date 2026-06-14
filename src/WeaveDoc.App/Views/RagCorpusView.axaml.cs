using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WeaveDoc.App.ViewModels;

namespace WeaveDoc.App.Views;

public partial class RagCorpusView : UserControl
{
    private readonly ObservableCollection<string> _filtered = [];
    private string _filter = string.Empty;

    public RagCorpusView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private RagTabViewModel? ViewModel => DataContext as RagTabViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        CorpusList.ItemsSource = _filtered;
        RebuildFilter();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.CorpusFiles.CollectionChanged -= OnCorpusChanged;
            vm.CorpusFiles.CollectionChanged += OnCorpusChanged;
            RebuildFilter();
        }
    }

    private void OnCorpusChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildFilter();

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _filter = SearchBox.Text ?? string.Empty;
        RebuildFilter();
    }

    private void RebuildFilter()
    {
        var source = ViewModel?.CorpusFiles;
        _filtered.Clear();
        if (source is null)
        {
            return;
        }

        var needle = _filter.Trim();
        foreach (var file in source)
        {
            if (string.IsNullOrEmpty(needle)
                || file.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                _filtered.Add(file);
            }
        }

        var hasCorpus = (ViewModel?.HasCorpus ?? false);
        EmptyState.IsVisible = _filtered.Count == 0;
        EmptyStateText.Text = !hasCorpus ? "暂无语料文件" : "无匹配文件";
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        var topLevel = TopLevel.GetTopLevel(this);
        if (vm is null || topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要加入知识库的文档",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown / 文本 / JSON")
                {
                    Patterns = ["*.md", "*.markdown", "*.txt", "*.json"]
                },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            await vm.AddDocumentFromPathAsync(file.Path.LocalPath);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            await vm.RefreshCorpusAsync();
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string path } && ViewModel is { } vm)
        {
            vm.SelectedDocument = path;
            await vm.DeleteSelectedDocumentAsync();
        }
    }
}
