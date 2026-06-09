using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using System;
using TextMateSharp.Grammars;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public readonly record struct NativeMarkdownSelection(int Start, int Length, string Text);

    public partial class NativeMarkdownEditorControl : UserControl, IDisposable
    {
        private readonly Func<RegistryOptions, string?> _markdownScopeResolver;
        private TextEditor? _editor;
        private TextMate.Installation? _textMateInstallation;
        private bool _isApplyingEditorContent;
        private bool _isDisposed;
        private bool _isPerformanceMode;
        private bool _hasUnsyncedContent;
        private bool _liveContentContainsMathMarkdown;

        private const int ExpensiveEditorFeatureContentLengthLimit = 32_000;
        private const int MathMarkerContextLength = 8;

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
            ConfigureEditor();
            _editor.Document.Changed += EditorDocument_Changed;
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
                _liveContentContainsMathMarkdown = false;

            if (!_liveContentContainsMathMarkdown && ChangeMayContainMathMarkdown(_editor.Document, e))
                _liveContentContainsMathMarkdown = true;

            ApplyPerformanceModeForState(contentLength, _liveContentContainsMathMarkdown);
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

            if (_editor == null)
                return;

            if (change.Property == EditorContentProperty)
            {
                if (_isApplyingEditorContent)
                    return;

                ApplyEditorContent(change.NewValue as string, updateStyledProperty: false);
                return;
            }

            if (change.Property == IsReadOnlyProperty)
                _editor.IsReadOnly = change.NewValue is bool isReadOnly && isReadOnly;
        }

        public void SetContent(string? content) => ApplyEditorContent(content, updateStyledProperty: true);

        public string GetContent() => NormalizeContent(_editor?.Text);

        public void InsertAtCursor(string prefix, string suffix) => WrapSelection(prefix, suffix);

        public void WrapSelection(string prefix, string suffix)
        {
            if (_editor == null || _editor.Document == null || _editor.IsReadOnly)
                return;

            prefix ??= string.Empty;
            suffix ??= string.Empty;

            var selection = GetSelection();
            var replacement = prefix + selection.Text + suffix;
            _editor.Document.Replace(selection.Start, selection.Length, replacement);
            SetSelection(selection.Start + prefix.Length, selection.Text.Length);
            FocusEditor();
        }

        public NativeMarkdownSelection GetSelection()
        {
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
            _editor.Select(safeStart, safeLength);
            _editor.CaretOffset = safeStart + safeLength;
        }

        public void SetCaretOffset(int offset)
        {
            if (_editor == null)
                return;

            var safeOffset = ClampOffset(offset);
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
            _editor?.Focus();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (_editor?.Document != null)
                _editor.Document.Changed -= EditorDocument_Changed;

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
            _editor.WordWrap = true;
        }

        private void ApplyEditorContent(string? content, bool updateStyledProperty)
        {
            if (_editor == null)
                return;

            var normalized = NormalizeContent(content);
            ApplyPerformanceModeForContent(normalized);

            using (StartProgrammaticEditorUpdate())
            {
                if (updateStyledProperty && !string.Equals(EditorContent, normalized, StringComparison.Ordinal))
                    EditorContent = normalized;

                if (!string.Equals(NormalizeContent(_editor.Text), normalized, StringComparison.Ordinal))
                    _editor.Text = normalized;
            }

            HasUnsyncedContent = false;
        }

        private void ApplyPerformanceModeForContent(string content)
        {
            _liveContentContainsMathMarkdown = ContainsMathMarkdown(content);
            ApplyPerformanceModeForState(content.Length, _liveContentContainsMathMarkdown);
        }

        private void ApplyPerformanceModeForState(int contentLength, bool containsMathMarkdown)
        {
            if (_editor == null)
                return;

            var shouldDisableExpensiveFeatures =
                contentLength > ExpensiveEditorFeatureContentLengthLimit || containsMathMarkdown;

            if (shouldDisableExpensiveFeatures)
            {
                var shouldDisableWordWrap = contentLength > ExpensiveEditorFeatureContentLengthLimit;
                var desiredWordWrap = !shouldDisableWordWrap;
                if (_isPerformanceMode && _textMateInstallation == null && _editor.WordWrap == desiredWordWrap)
                    return;

                _isPerformanceMode = true;
                _editor.WordWrap = desiredWordWrap;
                ReleaseTextMateInstallation(markReleased: false);
                IsMarkdownGrammarLoaded = false;
                MarkdownGrammarStatusText = shouldDisableWordWrap
                    ? "大 Markdown 文件已关闭语法高亮和自动换行，以保持编辑流畅。"
                    : "包含 LaTeX/数学片段的 Markdown 已关闭语法高亮，以保持编辑流畅。";
                return;
            }

            if (!_isPerformanceMode && _textMateInstallation != null)
                return;

            _isPerformanceMode = false;
            _editor.WordWrap = true;
            if (_textMateInstallation == null)
                TryInitializeMarkdownGrammar();
        }

        private bool TryInitializeMarkdownGrammar()
        {
            ReleaseTextMateInstallation(markReleased: false);

            if (_editor == null)
                return false;

            try
            {
                var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
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

        private int GetOffsetFromLineColumn(int lineNumber, int column)
        {
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

        private int GetLiveContentLength() => _editor?.Document?.TextLength ?? 0;

        private static string NormalizeContent(string? content) => content ?? string.Empty;

        private static bool ChangeMayContainMathMarkdown(TextDocument document, DocumentChangeEventArgs change)
        {
            if (change.InsertionLength <= 0 || document.TextLength == 0)
                return false;

            var start = Math.Max(0, change.Offset - MathMarkerContextLength);
            var end = Math.Min(document.TextLength, change.Offset + change.InsertionLength + MathMarkerContextLength);
            return end > start && ContainsMathMarkdown(document.GetText(start, end - start));
        }

        private static bool ContainsMathMarkdown(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;

            return content.Contains('$', StringComparison.Ordinal)
                || content.Contains(@"\begin{", StringComparison.Ordinal)
                || content.Contains(@"\[", StringComparison.Ordinal)
                || content.Contains(@"\(", StringComparison.Ordinal);
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
