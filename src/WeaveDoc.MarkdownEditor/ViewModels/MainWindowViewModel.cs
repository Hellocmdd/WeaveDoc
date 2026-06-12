using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IMarkdownRenderService _markdownRenderService = new MarkdigMarkdownRenderService();

        public MainWindowViewModel()
        {
            DisplayName = "未命名 Markdown";
            StatusText = "就绪";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _html = string.Empty;
        public string Html
        {
            get => _html;
            set
            {
                if (_html == value) return;
                _html = value;
                OnPropertyChanged(nameof(Html));
                OnPropertyChanged(nameof(PreviewHtml));
            }
        }

        private string _editorContent = string.Empty;
        public string EditorContent
        {
            get => _editorContent;
            set => SetEditorContent(value, updatePreview: false);
        }

        /// <summary>
        /// 用于预览的 HTML 内容
        /// </summary>
        public string PreviewHtml
        {
            get
            {
                return Html;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));

        /// <summary>
        /// 打开 Markdown 文件并加载内容
        /// </summary>
        /// <param name="filePath">Markdown 文件路径</param>
        public async Task<MarkdownFileOpenResult> OpenFile(string? filePath)
        {
            var result = await StorageFileOpenService.OpenMarkdownPathAsync(filePath).ConfigureAwait(true);
            ApplyOpenedMarkdown(result);
            return result;
        }

        public void ApplyOpenedMarkdown(MarkdownFileOpenResult result)
        {
            if (result.Succeeded)
            {
                SetEditorContent(result.Content, updatePreview: false);
                RefreshPreview();
                CurrentFilePath = result.FilePath;
                DisplayName = string.IsNullOrWhiteSpace(result.DisplayName)
                    ? "未命名 Markdown"
                    : result.DisplayName;
                StatusText = $"已打开：{DisplayName}";
                IsStatusError = false;
                return;
            }

            SetOpenFailure(result.ErrorMessage ?? "打开 Markdown 文件失败。");
        }

        public void SetOpenFailure(string message)
        {
            SetStatus(string.IsNullOrWhiteSpace(message) ? "打开文件失败。" : message, isError: true);
        }

        public void SetStatus(string message, bool isError = false)
        {
            StatusText = string.IsNullOrWhiteSpace(message) ? "就绪" : message;
            IsStatusError = isError;
        }

        /// <summary>
        /// 当前打开的文件路径
        /// </summary>
        private string? _currentFilePath;
        public string? CurrentFilePath
        {
            get => _currentFilePath;
            set
            {
                if (_currentFilePath == value) return;
                _currentFilePath = value;
                OnPropertyChanged();
            }
        }

        public void RefreshPreview()
        {
            Html = _markdownRenderService.RenderPreviewHtml(EditorContent);
        }

        private System.Threading.CancellationTokenSource? _debounceCts;

        public async System.Threading.Tasks.Task DebouncedRefreshPreview(int delayMs = 300)
        {
            _debounceCts?.Cancel();
            _debounceCts = new System.Threading.CancellationTokenSource();
            var token = _debounceCts.Token;
            try
            {
                await System.Threading.Tasks.Task.Delay(delayMs, token);
                RefreshPreview();
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
            }
        }

        private void SetEditorContent(string? value, bool updatePreview)
        {
            var normalizedContent = value ?? string.Empty;
            if (_editorContent == normalizedContent) return;

            _editorContent = normalizedContent;
            OnPropertyChanged(nameof(EditorContent));

            if (updatePreview)
                RefreshPreview();
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName == value) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        private bool _isStatusError;
        public bool IsStatusError
        {
            get => _isStatusError;
            set
            {
                if (_isStatusError == value) return;
                _isStatusError = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 保存 Markdown 文件
        /// </summary>
        /// <param name="filePath">保存文件的路径</param>
        public void SaveFile(string filePath)
        {
            try
            {
                System.IO.File.WriteAllText(filePath, EditorContent);
                CurrentFilePath = filePath;
                DisplayName = System.IO.Path.GetFileName(filePath);
                StatusText = $"已保存：{DisplayName}";
                IsStatusError = false;
            }
            catch (Exception ex)
            {
                StatusText = $"保存 Markdown 文件失败：{ex.Message}";
                IsStatusError = true;
            }
        }
    }
}
