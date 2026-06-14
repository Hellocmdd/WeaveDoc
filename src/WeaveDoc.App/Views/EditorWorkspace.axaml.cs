using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.App.Views;

public partial class EditorWorkspace : UserControl
{
    private AppShellViewModel? _subscribedViewModel;
    private NativeMarkdownEditorControl? _subscribedMarkdownEditor;

    public EditorWorkspace()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    private NativeMarkdownEditorControl? MarkdownEditor =>
        this.FindControl<NativeMarkdownEditorControl>("MarkdownEditorControl");

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToMarkdownEditor();
        SubscribeToViewModel(ViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeFromMarkdownEditor();
        UnsubscribeFromViewModel();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeToViewModel(ViewModel);
    }

    private void SubscribeToViewModel(AppShellViewModel? viewModel)
    {
        if (ReferenceEquals(_subscribedViewModel, viewModel))
        {
            SyncDocumentSnapshotToEditor();
            return;
        }

        UnsubscribeFromViewModel();
        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.DocumentWorkspace.PropertyChanged += OnDocumentWorkspacePropertyChanged;
        SyncDocumentSnapshotToEditor();
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.DocumentWorkspace.PropertyChanged -= OnDocumentWorkspacePropertyChanged;
        _subscribedViewModel = null;
    }

    private void OnDocumentWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentWorkspaceViewModel.Content)
            or nameof(DocumentWorkspaceViewModel.CurrentFilePath)
            or nameof(DocumentWorkspaceViewModel.HasDocument))
        {
            SyncDocumentSnapshotToEditor();
        }
    }

    private void SyncDocumentSnapshotToEditor()
    {
        var documentWorkspace = _subscribedViewModel?.DocumentWorkspace;
        if (documentWorkspace?.HasDocument != true)
            return;

        MarkdownEditor?.SetContent(documentWorkspace.Content);
    }

    /// <summary>
    /// Pushes the current editor text into <see cref="DocumentWorkspaceViewModel.Content"/>.
    /// Call before any save/export operation so the ViewModel has the latest text.
    /// </summary>
    public void SyncEditorContentToWorkspace()
    {
        var documentWorkspace = _subscribedViewModel?.DocumentWorkspace;
        var markdownEditor = MarkdownEditor;
        if (documentWorkspace?.HasDocument != true || markdownEditor is null)
            return;

        var content = markdownEditor.GetContent();
        documentWorkspace.Content = content;
        markdownEditor.AcceptCurrentContent();
    }

    private void SubscribeToMarkdownEditor()
    {
        var markdownEditor = MarkdownEditor;
        if (ReferenceEquals(_subscribedMarkdownEditor, markdownEditor))
        {
            return;
        }

        UnsubscribeFromMarkdownEditor();
        _subscribedMarkdownEditor = markdownEditor;
        if (_subscribedMarkdownEditor is not null)
        {
            _subscribedMarkdownEditor.ContentEdited += OnMarkdownEditorContentEdited;
        }
    }

    private void UnsubscribeFromMarkdownEditor()
    {
        if (_subscribedMarkdownEditor is null)
        {
            return;
        }

        _subscribedMarkdownEditor.ContentEdited -= OnMarkdownEditorContentEdited;
        _subscribedMarkdownEditor = null;
    }

    private void OnMarkdownEditorContentEdited(object? sender, EventArgs e)
    {
        _subscribedViewModel?.DocumentWorkspace.MarkEdited();
    }

    private void OnEditModeClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SelectEditorMode(EditorSurfaceMode.Edit);
    }

    private void OnPreviewModeClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = ViewModel;
        SyncEditorContentToWorkspace();
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
