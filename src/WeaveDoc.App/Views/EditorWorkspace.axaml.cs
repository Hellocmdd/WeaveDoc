using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WeaveDoc.App.Services.Documents;
using WeaveDoc.App.ViewModels;
using WeaveDoc.MarkdownEditor.Controls;

namespace WeaveDoc.App.Views;

public partial class EditorWorkspace : UserControl
{
    private const int AutoSaveDebounceMilliseconds = 2000;
    private const int AutoSaveMaxIntervalMilliseconds = 30000;

    private AppShellViewModel? _subscribedViewModel;
    private NativeMarkdownEditorControl? _subscribedMarkdownEditor;
    private CancellationTokenSource? _autoSaveCts;
    private DateTimeOffset _lastAutoSaveAt = DateTimeOffset.UtcNow;

    private static readonly KeyGesture ToggleWordWrapGesture = new(Key.Z, KeyModifiers.Alt);
    private static readonly KeyGesture ToggleEditorModeGesture = new(Key.V, KeyModifiers.Control | KeyModifiers.Shift);

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
        KeyDown += OnWorkspaceKeyDown;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelPendingAutoSave();
        UnsubscribeFromMarkdownEditor();
        UnsubscribeFromViewModel();
        KeyDown -= OnWorkspaceKeyDown;
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
        CancelPendingAutoSave();
        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.DocumentWorkspace.PropertyChanged += OnDocumentWorkspacePropertyChanged;
        SyncDocumentSnapshotToEditor();
        MarkdownEditor?.SetWordWrap(_subscribedViewModel.IsWordWrapEnabled);
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

    public void ConfigureCitationCompletion(CitationCompletionService? completionService)
    {
        var markdownEditor = MarkdownEditor;
        if (markdownEditor is null)
            return;

        markdownEditor.CitationCompletionProvider = completionService is null
            ? null
            : completionService.GetSuggestionsAsync;
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
            _subscribedMarkdownEditor.SetWordWrap(_subscribedViewModel?.IsWordWrapEnabled ?? false);
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
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        var documentWorkspace = _subscribedViewModel?.DocumentWorkspace;
        if (documentWorkspace?.HasDocument != true
            || string.IsNullOrWhiteSpace(documentWorkspace.CurrentFilePath))
        {
            return;
        }

        CancelPendingAutoSave();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;
        var elapsed = DateTimeOffset.UtcNow - _lastAutoSaveAt;
        var delay = elapsed >= TimeSpan.FromMilliseconds(AutoSaveMaxIntervalMilliseconds)
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(AutoSaveDebounceMilliseconds);

        _ = RunAutoSaveAsync(delay, token);
    }

    private async Task RunAutoSaveAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            SyncEditorContentToWorkspace();
            var documentWorkspace = _subscribedViewModel?.DocumentWorkspace;
            if (documentWorkspace is null)
            {
                return;
            }

            if (await documentWorkspace.AutoSaveAsync(cancellationToken))
            {
                _lastAutoSaveAt = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelPendingAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = null;
    }

    private void OnEditModeClick(object? sender, RoutedEventArgs e) => SelectEditorMode(EditorSurfaceMode.Edit);

    private void OnPreviewModeClick(object? sender, RoutedEventArgs e) => SelectEditorMode(EditorSurfaceMode.Preview);

    private void SelectEditorMode(EditorSurfaceMode mode)
    {
        var viewModel = ViewModel;
        if (mode == EditorSurfaceMode.Preview)
        {
            SyncEditorContentToWorkspace();
            viewModel?.DocumentWorkspace.RefreshPreview();
        }

        viewModel?.SelectEditorMode(mode);
    }

    private void ToggleEditorMode()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        SelectEditorMode(viewModel.IsEditModeSelected ? EditorSurfaceMode.Preview : EditorSurfaceMode.Edit);
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

    private void OnWordWrapToggleClick(object? sender, RoutedEventArgs e) => ToggleWordWrap();

    private void OnWorkspaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (ToggleWordWrapGesture.Matches(e))
        {
            ToggleWordWrap();
            e.Handled = true;
        }
        else if (ToggleEditorModeGesture.Matches(e))
        {
            ToggleEditorMode();
            e.Handled = true;
        }
    }

    private void ToggleWordWrap()
    {
        var viewModel = ViewModel;
        if (viewModel?.IsMarkdownEditorVisible != true)
            return;

        viewModel.IsWordWrapEnabled = !viewModel.IsWordWrapEnabled;
        MarkdownEditor?.SetWordWrap(viewModel.IsWordWrapEnabled);
    }

    private void ApplyEditorWrap(string prefix, string suffix)
    {
        var viewModel = ViewModel;
        if (viewModel?.IsMarkdownEditorVisible != true)
            return;

        MarkdownEditor?.WrapSelection(prefix, suffix);
    }

    /// <summary>
    /// 在当前光标处插入 Pandoc 引用 [@key]，供文献库 Tab 的"插入引用"调用。
    /// 经 MainWindow 转发（AI 面板与编辑器无直接通道）。
    /// </summary>
    public void InsertCitation(string citationKey)
    {
        var viewModel = ViewModel;
        if (viewModel?.IsMarkdownEditorVisible != true || string.IsNullOrEmpty(citationKey))
            return;

        MarkdownEditor?.InsertText($"[@{citationKey}]");
    }
}
