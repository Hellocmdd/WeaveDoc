using System.ComponentModel;
using System.Runtime.CompilerServices;
using WeaveDoc.MarkdownEditor.Services;

namespace WeaveDoc.MarkdownEditor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly MarkdownService _markdownService = new();

        public MainWindowViewModel()
        {
            // 设置默认的编辑器内容，这样右侧预览就会显示内容
            EditorContent = "# Hello WeaveDoc!\n\nStart typing markdown here...";
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
            }
        }

        private string _editorContent = string.Empty;
        public string EditorContent
        {
            get => _editorContent;
            set
            {
                if (_editorContent == value) return;
                _editorContent = value;
                // 每次编辑器内容变更时，更新预览 HTML
                Html = _markdownService.ConvertToHtml(_editorContent ?? string.Empty);
                OnPropertyChanged(nameof(EditorContent));
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
    }
}