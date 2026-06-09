using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.App.Views;

public partial class EditorWorkspace : UserControl
{
    public EditorWorkspace()
    {
        InitializeComponent();
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    private NativeMarkdownEditorControl? MarkdownEditor =>
        this.FindControl<NativeMarkdownEditorControl>("MarkdownEditorControl");

    private void OnEditModeClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectEditorMode(EditorSurfaceMode.Edit);
    }

    private void OnPreviewModeClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        viewModel?.DocumentWorkspace.RefreshPreview();
        viewModel?.SelectEditorMode(EditorSurfaceMode.Preview);
    }

    private void OnHeading1Click(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("# ", string.Empty);
    }

    private void OnHeading2Click(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("## ", string.Empty);
    }

    private void OnBoldClick(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("**", "**");
    }

    private void OnItalicClick(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("_", "_");
    }

    private void OnBulletListClick(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("- ", string.Empty);
    }

    private void OnTaskListClick(object? sender, RoutedEventArgs e)
    {
        ApplyEditorWrap("- [ ] ", string.Empty);
    }

    private void ApplyEditorWrap(string prefix, string suffix)
    {
        var viewModel = ViewModel;
        if (viewModel?.IsMarkdownEditorVisible != true)
            return;

        MarkdownEditor?.WrapSelection(prefix, suffix);
    }
}
