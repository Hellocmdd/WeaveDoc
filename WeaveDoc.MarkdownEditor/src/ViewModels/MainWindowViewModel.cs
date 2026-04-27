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