using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using WeaveDoc.App.ViewModels;
using WeaveDoc.Converter;
using WeaveDoc.Converter.Config;

namespace WeaveDoc.App.Views;

public partial class MainWindow : Window
{
    private const double DefaultAiPanelWidth = 300;
    private const double AiPanelMinWidth = 280;
    private const double SplitterWidth = 4;

    private readonly AppShellViewModel _viewModel;
    private double _lastExpandedAiPanelWidth = DefaultAiPanelWidth;

    public MainWindow() : this(null!, null!) { }

    public MainWindow(ConfigManager? configManager, DocumentConversionEngine? engine)
    {
        InitializeComponent();
        _viewModel = new AppShellViewModel();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnShellPropertyChanged;
        ApplyShellPalette(_viewModel.Theme);
        ApplyAiPanelLayout();
        UpdateStateClasses();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnShellPropertyChanged;
        base.OnClosed(e);
    }

    private void OnToggleAiPanelClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleAiPanel();
    }

    private void OnSelectAiChatTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Chat);
    }

    private void OnSelectAiLiteratureTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Literature);
    }

    private void OnSelectAiSnapshotTabClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAiPanelTab(AiPanelTabKind.Snapshot);
    }

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTheme();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppShellViewModel.IsAiPanelExpanded):
                ApplyAiPanelLayout();
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.EditorMode):
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.SelectedAiPanelTab):
                UpdateStateClasses();
                break;
            case nameof(AppShellViewModel.Theme):
                ApplyShellPalette(_viewModel.Theme);
                UpdateStateClasses();
                break;
        }
    }

    private void ApplyAiPanelLayout()
    {
        var columns = ShellWorkspace.ColumnDefinitions;
        var rightSplitterColumn = columns[3];
        var aiPanelColumn = columns[4];

        if (_viewModel.IsAiPanelExpanded)
        {
            aiPanelColumn.MinWidth = AiPanelMinWidth;
            aiPanelColumn.Width = new GridLength(Math.Max(AiPanelMinWidth, _lastExpandedAiPanelWidth));
            rightSplitterColumn.MinWidth = SplitterWidth;
            rightSplitterColumn.Width = new GridLength(SplitterWidth);
            RightWorkspaceSplitter.IsVisible = true;
            AiAssistantPanelControl.IsVisible = true;
            return;
        }

        var currentWidth = aiPanelColumn.Width.Value >= AiPanelMinWidth
            ? aiPanelColumn.Width.Value
            : aiPanelColumn.ActualWidth;
        if (currentWidth >= AiPanelMinWidth)
        {
            _lastExpandedAiPanelWidth = currentWidth;
        }

        AiAssistantPanelControl.IsVisible = false;
        RightWorkspaceSplitter.IsVisible = false;
        rightSplitterColumn.MinWidth = 0;
        rightSplitterColumn.Width = new GridLength(0);
        aiPanelColumn.MinWidth = 0;
        aiPanelColumn.Width = new GridLength(0);
    }

    private void UpdateStateClasses()
    {
        SetActive(AiShellCommandButton, _viewModel.IsAiPanelExpanded);
        SetActive(AiChatCommandButton, _viewModel.IsAiChatTabSelected);
        SetActive(AiLiteratureCommandButton, _viewModel.IsAiLiteratureTabSelected);
        SetActive(AiSnapshotCommandButton, _viewModel.IsAiSnapshotTabSelected);
        SetActive(ThemeMenuButton, _viewModel.Theme == ShellThemeKind.Dark);

        var editModeButton = EditorWorkspaceControl.FindControl<Button>("EditModeButton");
        var previewModeButton = EditorWorkspaceControl.FindControl<Button>("PreviewModeButton");
        SetActive(editModeButton, _viewModel.IsEditModeSelected);
        SetActive(previewModeButton, _viewModel.IsPreviewModeSelected);

        SetActive(AiAssistantPanelControl.FindControl<Button>("AiChatTabButton"), _viewModel.IsAiChatTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiLiteratureTabButton"), _viewModel.IsAiLiteratureTabSelected);
        SetActive(AiAssistantPanelControl.FindControl<Button>("AiSnapshotTabButton"), _viewModel.IsAiSnapshotTabSelected);
    }

    private static void SetActive(Button? button, bool isActive)
    {
        if (button is null)
        {
            return;
        }

        if (isActive)
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
            return;
        }

        button.Classes.Remove("active");
    }

    private static void ApplyShellPalette(ShellThemeKind theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = theme == ShellThemeKind.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        var palette = theme == ShellThemeKind.Dark ? DarkShellPalette : LightShellPalette;
        foreach (var (key, color) in palette)
        {
            SetBrushColor(application, key, color);
        }
    }

    private static void SetBrushColor(Application application, string key, string color)
    {
        if (application.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = Color.Parse(color);
            return;
        }

        application.Resources[key] = new SolidColorBrush(Color.Parse(color));
    }

    private static readonly IReadOnlyDictionary<string, string> DarkShellPalette = new Dictionary<string, string>
    {
        ["ShellBackgroundBrush"] = "#0D1117",
        ["ShellChromeBrush"] = "#161B22",
        ["ShellTitleBarBrush"] = "#0D1117",
        ["ShellPanelBrush"] = "#161B22",
        ["ShellCardBrush"] = "#21262D",
        ["ShellRaisedBrush"] = "#1B222D",
        ["ShellInputBrush"] = "#0F151D",
        ["ShellHoverBrush"] = "#21262D",
        ["ShellSelectedBrush"] = "#1D3557",
        ["ShellBorderBrush"] = "#30363D",
        ["ShellSubtleBorderBrush"] = "#21262D",
        ["ShellTextBrush"] = "#E6EDF3",
        ["ShellMutedTextBrush"] = "#8B949E",
        ["ShellDisabledTextBrush"] = "#6E7681",
        ["ShellAccentBrush"] = "#58A6FF",
        ["ShellAccentStrongBrush"] = "#1F6FEB",
        ["ShellSuccessBrush"] = "#3FB950",
        ["ShellWarningBrush"] = "#D29922",
        ["ShellEditorBackgroundBrush"] = "#0D1117",
        ["ShellEditorPanelBrush"] = "#161B22",
        ["ShellPaperWorkspaceBrush"] = "#21262D"
    };

    private static readonly IReadOnlyDictionary<string, string> LightShellPalette = new Dictionary<string, string>
    {
        ["ShellBackgroundBrush"] = "#FFFFFF",
        ["ShellChromeBrush"] = "#F8F9FA",
        ["ShellTitleBarBrush"] = "#161B22",
        ["ShellPanelBrush"] = "#F8F9FA",
        ["ShellCardBrush"] = "#FFFFFF",
        ["ShellRaisedBrush"] = "#EAEEF2",
        ["ShellInputBrush"] = "#EAEEF2",
        ["ShellHoverBrush"] = "#D8DEE4",
        ["ShellSelectedBrush"] = "#DDF4FF",
        ["ShellBorderBrush"] = "#D8DEE4",
        ["ShellSubtleBorderBrush"] = "#EAEEF2",
        ["ShellTextBrush"] = "#1C2128",
        ["ShellMutedTextBrush"] = "#57606A",
        ["ShellDisabledTextBrush"] = "#8B949E",
        ["ShellAccentBrush"] = "#0969DA",
        ["ShellAccentStrongBrush"] = "#0550AE",
        ["ShellSuccessBrush"] = "#1A7F37",
        ["ShellWarningBrush"] = "#9A6700",
        ["ShellEditorBackgroundBrush"] = "#0D1117",
        ["ShellEditorPanelBrush"] = "#161B22",
        ["ShellPaperWorkspaceBrush"] = "#EAEEF2"
    };
}
