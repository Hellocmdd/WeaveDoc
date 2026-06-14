using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WeaveDoc.App.ViewModels;
using WeaveDoc.MarkdownEditor.Controls;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.App.Views;

public partial class PdfWorkspace : UserControl
{
    /// <summary>
    /// Raised when the user requests to open another PDF from within the PDF workspace.
    /// The host (MainWindow) should subscribe and open the file picker.
    /// </summary>
    public event EventHandler? OpenPdfRequested;

    private PdfViewerControl? _pdfViewer;
    private bool _isInitialized;
    private string _lastPdfFilePath = string.Empty;
    private string? _temporaryPdfFilePath;

    public PdfWorkspace()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
            _ = HandleVisibilityChangedAsync(change.NewValue is true);
    }

    private AppShellViewModel? ViewModel => DataContext as AppShellViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _pdfViewer = this.FindControl<PdfViewerControl>("PdfViewerControl");

        // Wire ESC key for full-screen exit at window level
        if (TopLevel.GetTopLevel(this) is Window window)
            window.KeyDown += OnWindowKeyDown;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
            window.KeyDown -= OnWindowKeyDown;

        ReplaceTemporaryPdfFile(null);
        base.OnDetachedFromVisualTree(e);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pdfViewer?.IsFullScreen == true)
        {
            await _pdfViewer.ToggleFullScreen();
            e.Handled = true;
        }
    }

    private async Task HandleVisibilityChangedAsync(bool isVisible)
    {
        if (!isVisible)
        {
            // Going hidden: deactivate PDF viewer to free resources
            if (_pdfViewer != null)
                await _pdfViewer.DeactivateAsync();
            return;
        }

        // Becoming visible: load the PDF referenced in the view model
        var vm = ViewModel;
        if (vm == null) return;

        var filePath = vm.CurrentPdfPath;
        if (string.IsNullOrEmpty(filePath)) return;

        // Same file already loaded - just reactivate
        if (filePath == _lastPdfFilePath && _isInitialized)
        {
            if (_pdfViewer != null)
                await _pdfViewer.Activate();
            return;
        }

        await LoadPdfAsync(filePath);
    }

    /// <summary>
    /// Called from MainWindow after a PDF file has been selected and prepared.
    /// </summary>
    public async Task ShowPdfAsync(string filePath, string displayName, bool isTemporary)
    {
        ReplaceTemporaryPdfFile(isTemporary ? filePath : null);
        _lastPdfFilePath = filePath;

        // Update view model → triggers IsVisible if not already PDF mode
        if (ViewModel is AppShellViewModel vm)
            vm.OpenPdfMode(filePath, displayName);

        // If already visible, load directly (IsVisibleChanged won't fire again)
        if (IsVisible)
            await LoadPdfAsync(filePath);
    }

    private async Task LoadPdfAsync(string filePath)
    {
        if (_pdfViewer == null) return;

        if (!_isInitialized)
        {
            _isInitialized = true;
            await _pdfViewer.InitializeAsync();
        }
        else
        {
            await _pdfViewer.Activate();
        }

        await _pdfViewer.LoadPdfAsync(filePath);
    }

    private void OnClosePdfClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ClosePdfMode();
    }

    private async void OnFullScreenPdfClick(object? sender, RoutedEventArgs e)
    {
        if (_pdfViewer != null)
            await _pdfViewer.ToggleFullScreen();
    }

    private void OnOpenAnotherPdfClick(object? sender, RoutedEventArgs e)
    {
        OpenPdfRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceTemporaryPdfFile(string? filePath)
    {
        if (!string.Equals(_temporaryPdfFilePath, filePath, StringComparison.Ordinal))
            StorageFileOpenService.TryDeleteTemporaryFile(_temporaryPdfFilePath);

        _temporaryPdfFilePath = filePath;
    }
}
