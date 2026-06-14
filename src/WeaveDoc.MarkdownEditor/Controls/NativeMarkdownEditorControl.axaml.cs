using AvaloniaEdit.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using System;
using System.Diagnostics;
using TextMateSharp.Grammars;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public readonly record struct NativeMarkdownSelection(int Start, int Length, string Text);

    public partial class NativeMarkdownEditorControl : UserControl, IDisposable
    {
        private readonly Func<RegistryOptions, string?> _markdownScopeResolver;
        private TextEditor? _editor;
        private TextBox? _plainTextFallbackEditor;
        private TextMate.Installation? _textMateInstallation;
        private bool _isApplyingEditorContent;
        private bool _isDisposed;
        private bool _hasUnsyncedContent;
        private bool _isUsingPlainTextFallback;
        private bool _liveContentNeedsPlainTextFallback;
        private readonly bool _debugSelectionProbeEnabled = IsEnvironmentFlagEnabled(DebugSelectionProbeEnvironmentVariable);
        private readonly bool _debugForceAvaloniaEdit = IsEnvironmentFlagEnabled(DebugForceAvaloniaEditEnvironmentVariable);
        private readonly Stopwatch _debugSelectionStopwatch = new();
        private DispatcherTimer? _debugSelectionSampler;
        private int _debugPointerPressedCount;
        private int _debugPointerMovedCount;
        private int _debugPointerReleasedCount;
        private int _debugSelectionChangedCount;
        private int _debugCaretPositionChangedCount;
        private int _debugScrollOffsetChangedCount;
        private int _debugVisualLinesChangedCount;
        private int _debugTextViewLayoutUpdatedCount;
        private int _debugSamplePointerMovedCount;
        private int _debugSampleSelectionChangedCount;
        private int _debugSampleCaretChangedCount;
        private int _debugSampleScrollOffsetChangedCount;
        private int _debugSampleVisualLinesChangedCount;
        private int _debugSampleLayoutUpdatedCount;
        private long _debugLastPointerMoveLogMilliseconds;
        private int _debugLastPointerMoveLogCount;

        // Scroll-freeze loop guard (fixes AvaloniaEdit SelectionMouseHandler infinite loop)
        private double _lastScrollX = double.NaN;
        private int _scrollOscillationCount;
        // Fire the loop-breaker after this many identical-scrollX consecutive ScrollOffsetChanged events.
        // At ~40,000 events/sec during a freeze, 200 events = ~5ms to detect; safe threshold
        // for normal rapid scrolling which changes scrollX every event.
        private const int ScrollFreezeThreshold = 200;

        private static RegistryOptions? _cachedRegistryOptions;
        private static readonly object _registryOptionsLock = new();

        private const bool DefaultWordWrap = false;
        private const int DebugSelectionSampleIntervalMilliseconds = 250;
        private const string DebugPrefix = "[DEBUG-avedit-freeze]";
        private const string DebugSelectionProbeEnvironmentVariable = "WEAVEDOC_DEBUG_AVEDIT_SELECTION";
        private const string DebugForceAvaloniaEditEnvironmentVariable = "WEAVEDOC_DEBUG_FORCE_AVALONIAEDIT";

        public event EventHandler? ContentEdited;

        public static readonly StyledProperty<string> EditorContentProperty =
            AvaloniaProperty.Register<NativeMarkdownEditorControl, string>(
                nameof(EditorContent),
                string.Empty,
                defaultBindingMode: BindingMode.OneWay);

        public static readonly DirectProperty<NativeMarkdownEditorControl, bool> HasUnsyncedContentProperty =
            AvaloniaProperty.RegisterDirect<NativeMarkdownEditorControl, bool>(
                nameof(HasUnsyncedContent),
                owner => owner.HasUnsyncedContent);

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<NativeMarkdownEditorControl, bool>(nameof(IsReadOnly));

        public string EditorContent
        {
            get => GetValue(EditorContentProperty);
            set => SetValue(EditorContentProperty, value ?? string.Empty);
        }

        public bool HasUnsyncedContent
        {
            get => _hasUnsyncedContent;
            private set => SetAndRaise(HasUnsyncedContentProperty, ref _hasUnsyncedContent, value);
        }

        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public bool IsMarkdownGrammarLoaded { get; private set; }

        public bool IsUsingPlainTextFallback => _isUsingPlainTextFallback;

        public string MarkdownGrammarStatusText { get; private set; } = "Markdown 语法高亮尚未初始化。";

        public NativeMarkdownEditorControl() : this(ResolveMarkdownScope)
        {
        }

        public NativeMarkdownEditorControl(Func<RegistryOptions, string?> markdownScopeResolver)
        {
            _markdownScopeResolver = markdownScopeResolver ?? ResolveMarkdownScope;
            InitializeComponent();

            _editor = this.FindControl<TextEditor>("Editor")
                ?? throw new InvalidOperationException("Native Markdown editor TextEditor was not found.");
            _plainTextFallbackEditor = this.FindControl<TextBox>("PlainTextFallbackEditor")
                ?? throw new InvalidOperationException("Native Markdown editor plain text fallback was not found.");
            ConfigureEditor();
            _editor.Document.Changed += EditorDocument_Changed;
            _plainTextFallbackEditor.TextChanged += PlainTextFallbackEditor_TextChanged;
            ConfigureDebugSelectionProbe();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void EditorDocument_Changed(object? sender, DocumentChangeEventArgs e)
        {
            if (_editor?.Document == null || _isApplyingEditorContent)
                return;

            HasUnsyncedContent = true;
            var contentLength = _editor.Document.TextLength;
            if (contentLength == 0)
            {
                _liveContentNeedsPlainTextFallback = false;
            }
            else if (!_liveContentNeedsPlainTextFallback && ChangeMayNeedPlainTextFallback(_editor.Document, e))
            {
                _liveContentNeedsPlainTextFallback = true;
            }

            SuppressFallbackWhenDebugForcingAvaloniaEdit("document-change");
            ApplyPerformanceModeForState(contentLength, _liveContentNeedsPlainTextFallback);
            ContentEdited?.Invoke(this, EventArgs.Empty);
        }

        private void PlainTextFallbackEditor_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_plainTextFallbackEditor == null || _isApplyingEditorContent)
                return;

            HasUnsyncedContent = true;
            ContentEdited?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            ReleaseTextMateInstallation(markReleased: true);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (_editor == null || _plainTextFallbackEditor == null)
                return;

            if (change.Property == EditorContentProperty)
            {
                if (_isApplyingEditorContent)
                    return;

                ApplyEditorContent(change.NewValue as string, updateStyledProperty: false);
                return;
            }

            if (change.Property == IsReadOnlyProperty)
            {
                var isReadOnly = change.NewValue is bool value && value;
                _editor.IsReadOnly = isReadOnly;
                _plainTextFallbackEditor.IsReadOnly = isReadOnly;
            }
        }

        public void SetContent(string? content) => ApplyEditorContent(content, updateStyledProperty: true);

        public void AcceptCurrentContent()
        {
            var content = GetContent();
            ApplyEditorContent(content, updateStyledProperty: true);
            Dispatcher.UIThread.Post(() =>
            {
                if (string.Equals(GetContent(), EditorContent, StringComparison.Ordinal))
                {
                    HasUnsyncedContent = false;
                }
            }, DispatcherPriority.Background);
        }

        public string GetContent() => NormalizeContent(_isUsingPlainTextFallback
            ? _plainTextFallbackEditor?.Text
            : _editor?.Text);

        public void InsertAtCursor(string prefix, string suffix) => WrapSelection(prefix, suffix);

        public void WrapSelection(string prefix, string suffix)
        {
            if (IsActiveEditorReadOnly())
                return;

            prefix ??= string.Empty;
            suffix ??= string.Empty;

            var selection = GetSelection();
            var replacement = prefix + selection.Text + suffix;
            ReplaceActiveSelection(selection.Start, selection.Length, replacement);
            SetSelection(selection.Start + prefix.Length, selection.Text.Length);
            FocusEditor();
        }

        public NativeMarkdownSelection GetSelection()
        {
            if (_isUsingPlainTextFallback)
            {
                if (_plainTextFallbackEditor == null)
                    return new NativeMarkdownSelection(0, 0, string.Empty);

                var text = NormalizeContent(_plainTextFallbackEditor.Text);
                var fallbackStart = ClampOffset(_plainTextFallbackEditor.SelectionStart);
                var fallbackSelectionEnd = Math.Clamp(_plainTextFallbackEditor.SelectionEnd, fallbackStart, GetLiveContentLength());
                var fallbackLength = fallbackSelectionEnd - fallbackStart;
                var selectedText = fallbackLength == 0
                    ? string.Empty
                    : text.Substring(fallbackStart, fallbackLength);
                return new NativeMarkdownSelection(fallbackStart, fallbackLength, selectedText);
            }

            if (_editor == null)
                return new NativeMarkdownSelection(0, 0, string.Empty);

            var start = ClampOffset(_editor.SelectionStart);
            var length = Math.Clamp(_editor.SelectionLength, 0, GetLiveContentLength() - start);
            return new NativeMarkdownSelection(start, length, _editor.SelectedText ?? string.Empty);
        }

        public void SetSelection(int start, int length)
        {
            if (_editor == null)
                return;

            var textLength = GetLiveContentLength();
            var safeStart = Math.Clamp(start, 0, textLength);
            var safeLength = Math.Clamp(length, 0, textLength - safeStart);

            if (_isUsingPlainTextFallback)
            {
                if (_plainTextFallbackEditor == null)
                    return;

                _plainTextFallbackEditor.SelectionStart = safeStart;
                _plainTextFallbackEditor.SelectionEnd = safeStart + safeLength;
                if (safeLength == 0)
                {
                    _plainTextFallbackEditor.CaretIndex = safeStart;
                }
                return;
            }

            if (_editor == null)
                return;

            _editor.Select(safeStart, safeLength);
            _editor.CaretOffset = safeStart + safeLength;
        }

        public void SetCaretOffset(int offset)
        {
            if (_editor == null && _plainTextFallbackEditor == null)
                return;

            var safeOffset = ClampOffset(offset);
            if (_isUsingPlainTextFallback)
            {
                if (_plainTextFallbackEditor == null)
                    return;

                _plainTextFallbackEditor.SelectionStart = safeOffset;
                _plainTextFallbackEditor.SelectionEnd = safeOffset;
                _plainTextFallbackEditor.CaretIndex = safeOffset;
                return;
            }

            if (_editor == null)
                return;

            _editor.Select(safeOffset, 0);
            _editor.CaretOffset = safeOffset;
            _editor.TextArea.Caret.BringCaretToView();
        }

        public void SetCaretPosition(int lineNumber, int column)
        {
            SetCaretOffset(GetOffsetFromLineColumn(lineNumber, column));
        }

        public void RevealLine(int lineNumber)
        {
            if (_isUsingPlainTextFallback)
                return;

            if (_editor == null || _editor.Document == null || _editor.Document.LineCount == 0)
                return;

            _editor.ScrollToLine(ClampLineNumber(lineNumber));
        }

        public void ScrollToPosition(int lineNumber, int column, int selectionLength = 0)
        {
            if (_editor == null)
                return;

            var offset = GetOffsetFromLineColumn(lineNumber, column);
            if (selectionLength > 0)
            {
                SetSelection(offset, selectionLength);
            }
            else
            {
                SetCaretOffset(offset);
            }

            RevealLine(lineNumber);
        }

        public void SetFocus() => FocusEditor();

        public void FocusEditor()
        {
            if (_isUsingPlainTextFallback)
                _plainTextFallbackEditor?.Focus();
            else
                _editor?.Focus();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_editor?.Document != null)
                _editor.Document.Changed -= EditorDocument_Changed;
            if (_plainTextFallbackEditor != null)
                _plainTextFallbackEditor.TextChanged -= PlainTextFallbackEditor_TextChanged;
            if (_editor != null)
                _editor.TextArea.TextView.ScrollOffsetChanged -= OnTextViewScrollOffsetChanged;

            _debugSelectionSampler?.Stop();
            _debugSelectionSampler = null;

            ReleaseTextMateInstallation(markReleased: true);
            GC.SuppressFinalize(this);
        }

        private void ConfigureEditor()
        {
            if (_editor == null)
                return;

            _editor.Options.ConvertTabsToSpaces = true;
            _editor.Options.IndentationSize = 4;
            _editor.IsReadOnly = IsReadOnly;
            _editor.WordWrap = DefaultWordWrap;

            if (_plainTextFallbackEditor != null)
            {
                _plainTextFallbackEditor.IsReadOnly = IsReadOnly;
                _plainTextFallbackEditor.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
            }

            // Fix for AvaloniaEdit infinite layout loop (confirmed by diagnostic logs):
            // When caret is at the end of a long line, ScrollOffsetChanged fires SelectionMouseHandler
            // which rebuilds visual lines, which triggers ArrangeOverride, which fires ScrollOffsetChanged
            // again — all with scrollX staying constant. We detect N identical-scrollX consecutive events
            // and temporarily zero out SelectionMouseHandler._mode via reflection to break the cycle.
            _editor.TextArea.TextView.ScrollOffsetChanged += OnTextViewScrollOffsetChanged;
        }

        // Breaks the SelectionMouseHandler ↔ VisualLines ↔ ArrangeOverride infinite loop.
        // Diagnostic logs show scrollX stays CONSTANT while ScrollOffsetChanged fires ~40,000/sec.
        // The loop is: ScrollOffsetChanged → ExtendSelectionToMouse → visual-line rebuild →
        //              ArrangeOverride → ScrollOffsetChanged (scrollX unchanged each round).
        // Fix: when ScrollOffsetChanged fires with the same scrollX for
        // ScrollFreezeThreshold consecutive times, we use reflection to set
        // SelectionMouseHandler._mode = None for one Dispatcher.Post cycle.
        // SelectionMouseHandler.TextView_ScrollOffsetChanged guards on _mode, so it
        // skips ExtendSelectionToMouse, visual lines stay valid, ArrangeOverride
        // finds nothing to do, and the loop dies naturally.
        private void OnTextViewScrollOffsetChanged(object? sender, EventArgs e)
        {
            if (_editor == null)
                return;

            var textView = _editor.TextArea.TextView;
            var currentScrollX = textView.ScrollOffset.X;

            // Detect constant-scrollX loop: scrollX identical for N+ consecutive events
            if (!double.IsNaN(_lastScrollX) && Math.Abs(currentScrollX - _lastScrollX) < 0.01)
            {
                _scrollOscillationCount++;
                if (_scrollOscillationCount == ScrollFreezeThreshold)
                {
                    // Break the loop by temporarily clearing SelectionMouseHandler._mode.
                    // This causes its ScrollOffsetChanged handler to bail out for one cycle.
                    BreakSelectionHandlerLoop();
                }
            }
            else
            {
                _scrollOscillationCount = 0;
                _lastScrollX = currentScrollX;
            }
        }

        private void BreakSelectionHandlerLoop()
        {
            if (_editor == null) return;
            try
            {
                var textArea = _editor.TextArea;

                // TextArea.DefaultInputHandler is public; MouseSelection is public ITextAreaInputHandler.
                // Only _mode (private enum) needs reflection.
                var mouseSelection = textArea.DefaultInputHandler?.MouseSelection;
                if (mouseSelection == null) return;

                var modeField = mouseSelection.GetType().GetField(
                    "_mode",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (modeField == null) return;

                // Set mode to None — SelectionMouseHandler.TextView_ScrollOffsetChanged guards on
                // _mode and skips ExtendSelectionToMouse when None, breaking the layout loop.
                // The next PointerPressed event sets _mode correctly via TextArea_MouseLeftButtonDown.
                modeField.SetValue(mouseSelection, 0); // SelectionMode.None == 0
                _scrollOscillationCount = 0;
                _lastScrollX = double.NaN;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BreakSelectionHandlerLoop] {ex.Message}");
                _scrollOscillationCount = 0;
            }
        }

        private void ApplyEditorContent(string? content, bool updateStyledProperty)
        {
            if (_editor == null || _plainTextFallbackEditor == null)
                return;

            var normalized = NormalizeContent(content);
            var textChanged = !string.Equals(GetContent(), normalized, StringComparison.Ordinal);
            if (textChanged)
                ApplyPerformanceModeForContent(normalized);

            using (StartProgrammaticEditorUpdate())
            {
                if (updateStyledProperty && !string.Equals(EditorContent, normalized, StringComparison.Ordinal))
                    EditorContent = normalized;

                if (textChanged)
                    SetActiveEditorText(normalized);
            }

            HasUnsyncedContent = false;
        }

        private void ApplyPerformanceModeForContent(string content)
        {
            _liveContentNeedsPlainTextFallback = NeedsPlainTextFallback(content);
            SuppressFallbackWhenDebugForcingAvaloniaEdit("content-load");
            ApplyPerformanceModeForState(content.Length, _liveContentNeedsPlainTextFallback);
        }

        private void ApplyPerformanceModeForState(int contentLength, bool needsPlainTextFallback)
        {
            if (_editor == null || _plainTextFallbackEditor == null)
                return;

            _editor.WordWrap = DefaultWordWrap;
            if (_debugForceAvaloniaEdit && needsPlainTextFallback)
            {
                DebugLogState("fallback-suppressed", "reason=apply-performance-mode");
                needsPlainTextFallback = false;
            }

            if (needsPlainTextFallback)
            {
                ReleaseTextMateInstallation(markReleased: false);
                IsMarkdownGrammarLoaded = false;
                MarkdownGrammarStatusText = "检测到 display-math / LaTeX 符号行，已切换为纯文本编辑模式并保留横向滚动，以避免拖选卡死。";
                SetPlainTextFallbackMode(true);
                return;
            }

            SetPlainTextFallbackMode(false);

            if (_textMateInstallation != null)
                return;

            if (contentLength > 0)
                TryInitializeMarkdownGrammar();
        }

        private bool TryInitializeMarkdownGrammar()
        {
            ReleaseTextMateInstallation(markReleased: false);

            if (_editor == null)
                return false;

            try
            {
                var registryOptions = GetOrCreateRegistryOptions();
                var scopeName = _markdownScopeResolver(registryOptions);
                if (string.IsNullOrWhiteSpace(scopeName))
                    throw new InvalidOperationException("Markdown grammar scope was not found.");

                _textMateInstallation = _editor.InstallTextMate(registryOptions);
                _textMateInstallation.SetGrammar(scopeName);
                IsMarkdownGrammarLoaded = true;
                MarkdownGrammarStatusText = "Markdown 语法高亮已加载。";
                return true;
            }
            catch (Exception ex)
            {
                ReleaseTextMateInstallation(markReleased: false);
                IsMarkdownGrammarLoaded = false;
                MarkdownGrammarStatusText = $"Markdown 语法高亮不可用，已降级为纯文本编辑：{ex.Message}";
                return false;
            }
        }

        private void ReleaseTextMateInstallation(bool markReleased)
        {
            if (_textMateInstallation == null)
                return;

            _textMateInstallation.Dispose();
            _textMateInstallation = null;

            if (markReleased)
            {
                IsMarkdownGrammarLoaded = false;
                MarkdownGrammarStatusText = "Markdown 语法高亮已释放。";
            }
        }

        private void SetPlainTextFallbackMode(bool enabled)
        {
            if (_editor == null || _plainTextFallbackEditor == null || _isUsingPlainTextFallback == enabled)
                return;

            var text = GetContent();
            using (StartProgrammaticEditorUpdate())
            {
                if (enabled)
                {
                    _plainTextFallbackEditor.Text = text;
                    _editor.Text = text;
                    _plainTextFallbackEditor.IsVisible = true;
                    _editor.IsVisible = false;
                }
                else
                {
                    _editor.Text = text;
                    _plainTextFallbackEditor.Text = text;
                    _editor.IsVisible = true;
                    _plainTextFallbackEditor.IsVisible = false;
                }
            }

            _isUsingPlainTextFallback = enabled;
        }

        private void ConfigureDebugSelectionProbe()
        {
            if (!_debugSelectionProbeEnabled || _editor == null)
                return;

            _debugSelectionStopwatch.Start();

            _editor.AddHandler(
                InputElement.PointerPressedEvent,
                DebugEditorPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            _editor.AddHandler(
                InputElement.PointerMovedEvent,
                DebugEditorPointerMoved,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);
            _editor.AddHandler(
                InputElement.PointerReleasedEvent,
                DebugEditorPointerReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true);

            _editor.TextArea.SelectionChanged += (_, _) =>
            {
                _debugSelectionChangedCount++;
                DebugLogSampledEvent("selection-changed", _debugSelectionChangedCount);
            };
            _editor.TextArea.Caret.PositionChanged += (_, _) =>
            {
                _debugCaretPositionChangedCount++;
                DebugLogSampledEvent("caret-position-changed", _debugCaretPositionChangedCount);
            };
            _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) =>
            {
                _debugScrollOffsetChangedCount++;
                DebugLogSampledEvent("scroll-offset-changed", _debugScrollOffsetChangedCount);
            };
            _editor.TextArea.TextView.VisualLinesChanged += (_, _) =>
            {
                _debugVisualLinesChangedCount++;
                DebugLogSampledEvent("visual-lines-changed", _debugVisualLinesChangedCount);
            };
            _editor.TextArea.TextView.LayoutUpdated += (_, _) =>
            {
                _debugTextViewLayoutUpdatedCount++;
                DebugLogSampledEvent("text-view-layout-updated", _debugTextViewLayoutUpdatedCount);
            };

            _debugSelectionSampler = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebugSelectionSampleIntervalMilliseconds)
            };
            _debugSelectionSampler.Tick += (_, _) => DebugLogState("sample", DebugBuildSampleDeltaText());
            _debugSelectionSampler.Start();

            DebugLogState(
                "probe-start",
                FormattableString.Invariant(
                    $"env={DebugSelectionProbeEnvironmentVariable} forceAvaloniaEdit={_debugForceAvaloniaEdit}"));
        }

        private void DebugEditorPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _debugPointerPressedCount++;
            DebugLogPointerEvent("pointer-pressed", e);
        }

        private void DebugEditorPointerMoved(object? sender, PointerEventArgs e)
        {
            _debugPointerMovedCount++;
            var elapsedMilliseconds = _debugSelectionStopwatch.ElapsedMilliseconds;
            if (elapsedMilliseconds - _debugLastPointerMoveLogMilliseconds < DebugSelectionSampleIntervalMilliseconds)
                return;

            var delta = _debugPointerMovedCount - _debugLastPointerMoveLogCount;
            _debugLastPointerMoveLogMilliseconds = elapsedMilliseconds;
            _debugLastPointerMoveLogCount = _debugPointerMovedCount;
            DebugLogPointerEvent("pointer-moved", e, FormattableString.Invariant($"deltaMoves={delta}"));
        }

        private void DebugEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _debugPointerReleasedCount++;
            DebugLogPointerEvent("pointer-released", e);
        }

        private void DebugLogPointerEvent(string source, PointerEventArgs e, string? extraDetail = null)
        {
            if (!_debugSelectionProbeEnabled || _editor == null)
                return;

            var position = e.GetPosition(_editor);
            var point = e.GetCurrentPoint(_editor);
            var detail = FormattableString.Invariant(
                $"x={position.X:0.###} y={position.Y:0.###} left={point.Properties.IsLeftButtonPressed} handled={e.Handled}");
            if (!string.IsNullOrWhiteSpace(extraDetail))
                detail += " " + extraDetail;

            DebugLogState(source, detail);
        }

        private void DebugLogSampledEvent(string source, int count)
        {
            var interval = source switch
            {
                "scroll-offset-changed" or "visual-lines-changed" => 1000,
                "text-view-layout-updated" => 100,
                _ => 25
            };
            if (count <= 5 || count % interval == 0)
                DebugLogState(source, FormattableString.Invariant($"count={count}"));
        }

        private string DebugBuildSampleDeltaText()
        {
            var pointerMovedDelta = _debugPointerMovedCount - _debugSamplePointerMovedCount;
            var selectionDelta = _debugSelectionChangedCount - _debugSampleSelectionChangedCount;
            var caretDelta = _debugCaretPositionChangedCount - _debugSampleCaretChangedCount;
            var scrollDelta = _debugScrollOffsetChangedCount - _debugSampleScrollOffsetChangedCount;
            var visualDelta = _debugVisualLinesChangedCount - _debugSampleVisualLinesChangedCount;
            var layoutDelta = _debugTextViewLayoutUpdatedCount - _debugSampleLayoutUpdatedCount;

            _debugSamplePointerMovedCount = _debugPointerMovedCount;
            _debugSampleSelectionChangedCount = _debugSelectionChangedCount;
            _debugSampleCaretChangedCount = _debugCaretPositionChangedCount;
            _debugSampleScrollOffsetChangedCount = _debugScrollOffsetChangedCount;
            _debugSampleVisualLinesChangedCount = _debugVisualLinesChangedCount;
            _debugSampleLayoutUpdatedCount = _debugTextViewLayoutUpdatedCount;

            return FormattableString.Invariant(
                $"deltaPointerMoved={pointerMovedDelta} deltaSelection={selectionDelta} deltaCaret={caretDelta} deltaScroll={scrollDelta} deltaVisualLines={visualDelta} deltaLayout={layoutDelta}");
        }

        private void DebugLogState(string source, string? detail = null)
        {
            if (!_debugSelectionProbeEnabled || _editor == null)
                return;

            var textView = _editor.TextArea.TextView;
            var scrollOffset = textView.ScrollOffset;
            var visualLineCount = DebugGetVisualLineCount(textView);
            var elapsedMilliseconds = _debugSelectionStopwatch.ElapsedMilliseconds;
            var line = FormattableString.Invariant(
                $"{DebugPrefix} elapsed_ms={elapsedMilliseconds} source={source} pressed={_debugPointerPressedCount} moved={_debugPointerMovedCount} released={_debugPointerReleasedCount} selectionChanged={_debugSelectionChangedCount} caretChanged={_debugCaretPositionChangedCount} scrollChanged={_debugScrollOffsetChangedCount} visualLinesChanged={_debugVisualLinesChangedCount} layoutUpdated={_debugTextViewLayoutUpdatedCount} selectionStart={_editor.SelectionStart} selectionLength={_editor.SelectionLength} caretOffset={_editor.CaretOffset} scrollX={scrollOffset.X:0.###} scrollY={scrollOffset.Y:0.###} visualLines={visualLineCount} textLength={_editor.Document?.TextLength ?? -1} fallback={_isUsingPlainTextFallback} forceAvaloniaEdit={_debugForceAvaloniaEdit}");
            if (!string.IsNullOrWhiteSpace(detail))
                line += " " + detail;

            Console.Error.WriteLine(line);
        }

        private static int DebugGetVisualLineCount(AvaloniaEdit.Rendering.TextView textView)
        {
            try
            {
                return textView.VisualLines?.Count ?? -1;
            }
            catch (AvaloniaEdit.Rendering.VisualLinesInvalidException)
            {
                return -2;
            }
        }

        private void SuppressFallbackWhenDebugForcingAvaloniaEdit(string reason)
        {
            if (!_debugForceAvaloniaEdit || !_liveContentNeedsPlainTextFallback)
                return;

            _liveContentNeedsPlainTextFallback = false;
            DebugLogState("fallback-suppressed", FormattableString.Invariant($"reason={reason}"));
        }

        private void SetActiveEditorText(string text)
        {
            if (_editor == null || _plainTextFallbackEditor == null)
                return;

            if (_isUsingPlainTextFallback)
            {
                _plainTextFallbackEditor.Text = text;
                _editor.Text = text;
            }
            else
            {
                _editor.Text = text;
                _plainTextFallbackEditor.Text = text;
            }
        }

        private bool IsActiveEditorReadOnly()
        {
            return _isUsingPlainTextFallback
                ? _plainTextFallbackEditor?.IsReadOnly != false
                : _editor?.IsReadOnly != false || _editor.Document == null;
        }

        private void ReplaceActiveSelection(int start, int length, string replacement)
        {
            if (_isUsingPlainTextFallback)
            {
                var content = GetContent();
                var safeStart = Math.Clamp(start, 0, content.Length);
                var safeLength = Math.Clamp(length, 0, content.Length - safeStart);
                SetActiveEditorText(content.Remove(safeStart, safeLength).Insert(safeStart, replacement));
                return;
            }

            _editor?.Document?.Replace(start, length, replacement);
        }

        private int GetOffsetFromLineColumn(int lineNumber, int column)
        {
            if (_isUsingPlainTextFallback)
                return GetOffsetFromLineColumn(GetContent(), lineNumber, column);

            if (_editor?.Document == null || _editor.Document.LineCount == 0)
                return 0;

            var line = _editor.Document.GetLineByNumber(ClampLineNumber(lineNumber));
            var columnOffset = Math.Clamp(column - 1, 0, line.Length);
            return line.Offset + columnOffset;
        }

        private int ClampLineNumber(int lineNumber)
        {
            if (_editor?.Document == null || _editor.Document.LineCount == 0)
                return 1;

            return Math.Clamp(lineNumber, 1, _editor.Document.LineCount);
        }

        private int ClampOffset(int offset) => Math.Clamp(offset, 0, GetLiveContentLength());

        private IDisposable StartProgrammaticEditorUpdate() => new ProgrammaticEditorUpdateScope(this);

        private int GetLiveContentLength() => _isUsingPlainTextFallback
            ? NormalizeContent(_plainTextFallbackEditor?.Text).Length
            : _editor?.Document?.TextLength ?? 0;

        private static string NormalizeContent(string? content) => content ?? string.Empty;

        private static bool IsEnvironmentFlagEnabled(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetOffsetFromLineColumn(string content, int lineNumber, int column)
        {
            if (string.IsNullOrEmpty(content))
                return 0;

            var targetLine = Math.Max(1, lineNumber);
            var currentLine = 1;
            var lineStart = 0;

            while (currentLine < targetLine && lineStart < content.Length)
            {
                var lineEnd = content.IndexOfAny(['\r', '\n'], lineStart);
                if (lineEnd < 0)
                    return content.Length;

                lineStart = lineEnd + 1;
                if (lineEnd + 1 < content.Length && content[lineEnd] == '\r' && content[lineEnd + 1] == '\n')
                    lineStart++;

                currentLine++;
            }

            var nextLineBreak = content.IndexOfAny(['\r', '\n'], lineStart);
            if (nextLineBreak < 0)
                nextLineBreak = content.Length;

            return lineStart + Math.Clamp(column - 1, 0, nextLineBreak - lineStart);
        }

        private static bool NeedsPlainTextFallback(TextDocument document)
        {
            foreach (var line in document.Lines)
            {
                if (NeedsPlainTextFallback(document, line))
                    return true;
            }

            return false;
        }

        private static bool ChangeMayNeedPlainTextFallback(TextDocument document, DocumentChangeEventArgs change)
        {
            if (document.TextLength == 0 || change.InsertionLength <= 0)
                return false;

            var startOffset = Math.Clamp(change.Offset, 0, document.TextLength);
            var endOffset = Math.Clamp(change.Offset + change.InsertionLength, 0, document.TextLength);
            var line = document.GetLineByOffset(startOffset);
            var endLine = document.GetLineByOffset(endOffset);

            while (line != null)
            {
                if (NeedsPlainTextFallback(document, line))
                    return true;

                if (line == endLine)
                    break;

                line = line.NextLine;
            }

            return false;
        }

        private static bool NeedsPlainTextFallback(TextDocument document, DocumentLine line)
        {
            return NeedsPlainTextFallback(document.GetText(line.Offset, line.Length));
        }

        private static bool NeedsPlainTextFallback(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            var lineStart = 0;
            while (lineStart < content.Length)
            {
                var lineEnd = content.IndexOfAny(['\r', '\n'], lineStart);
                if (lineEnd < 0)
                    lineEnd = content.Length;

                if (LineNeedsPlainTextFallback(content.AsSpan(lineStart, lineEnd - lineStart)))
                {
                    return true;
                }

                if (lineEnd >= content.Length)
                    break;

                lineStart = lineEnd + 1;
                if (lineEnd + 1 < content.Length && content[lineEnd] == '\r' && content[lineEnd + 1] == '\n')
                    lineStart++;
            }

            return false;
        }

        private static bool LineNeedsPlainTextFallback(ReadOnlySpan<char> line)
        {
            line = line.Trim();
            return IsDisplayMathLine(line)
                || line.Contains(@"\begin{".AsSpan(), StringComparison.Ordinal)
                || line.Contains(@"\[".AsSpan(), StringComparison.Ordinal);
        }

        private static bool IsDisplayMathLine(ReadOnlySpan<char> line)
        {
            return line.Length >= 4 && line.StartsWith("$$".AsSpan(), StringComparison.Ordinal);
        }

        private static RegistryOptions GetOrCreateRegistryOptions()
        {
            if (_cachedRegistryOptions != null)
                return _cachedRegistryOptions;

            lock (_registryOptionsLock)
            {
                if (_cachedRegistryOptions == null)
                    _cachedRegistryOptions = new RegistryOptions(ThemeName.DarkPlus);
            }

            return _cachedRegistryOptions;
        }

        private static string? ResolveMarkdownScope(RegistryOptions registryOptions)
        {
            var markdown = registryOptions.GetLanguageByExtension(".md");
            return registryOptions.GetScopeByLanguageId(markdown.Id);
        }

        private sealed class ProgrammaticEditorUpdateScope : IDisposable
        {
            private readonly NativeMarkdownEditorControl _owner;
            private readonly bool _wasApplyingEditorContent;
            private bool _isDisposed;

            public ProgrammaticEditorUpdateScope(NativeMarkdownEditorControl owner)
            {
                _owner = owner;
                _wasApplyingEditorContent = owner._isApplyingEditorContent;
                owner._isApplyingEditorContent = true;
            }

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _owner._isApplyingEditorContent = _wasApplyingEditorContent;
                _isDisposed = true;
            }
        }
    }
}
