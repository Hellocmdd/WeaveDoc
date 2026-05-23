using System;
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
            // 设置 EditorContent 属性，这样右侧预览就会显示由 MarkdownService 生成的 HTML 内容
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
                OnPropertyChanged(nameof(PreviewHtml));
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
                OnPropertyChanged(nameof(EditorContent));
                // 每次编辑器内容变更时，更新预览 HTML（使用带字符位置信息的版本）
                var html = _markdownService.ConvertMarkdownToHtmlWithCharPositions(_editorContent ?? string.Empty);
                Console.WriteLine($"=== Markdown转HTML结果 ===");
                Console.WriteLine($"行数: {html.Split('\n').Length}");
                Console.WriteLine($"包含 math-inline: {html.Contains("math-inline")}");
                Console.WriteLine($"包含 math-display: {html.Contains("math-display")}");
                // 如果包含LaTeX，输出前500字符
                if (html.Contains("math"))
                {
                    var mathIndex = html.IndexOf("math");
                    var start = Math.Max(0, mathIndex - 50);
                    var length = Math.Min(500, html.Length - start);
                    Console.WriteLine($"LaTeX相关HTML片段: ...{html.Substring(start, length)}...");
                }
                Console.WriteLine($"=== HTML内容开始 ===");
                Console.WriteLine(html.Substring(0, Math.Min(2000, html.Length)));
                Console.WriteLine($"=== HTML内容结束 ===");
                Html = html;
            }
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
        public void OpenFile(string filePath)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var content = System.IO.File.ReadAllText(filePath);
                    EditorContent = content;
                    CurrentFilePath = filePath;
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                System.Console.WriteLine($"打开文件时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 当前打开的文件路径
        /// </summary>
        public string? CurrentFilePath { get; set; }
        
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
            }
            catch (Exception ex)
            {
                // 处理异常
                System.Console.WriteLine($"保存文件时出错: {ex.Message}");
            }
        }
    }
}