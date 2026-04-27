using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 将 MainWindowViewModel 设置为 DataContext
            var vm = new MainWindowViewModel();
            DataContext = vm;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        
        /// <summary>
        /// 打开 Markdown 文件
        /// </summary>
        /// <returns></returns>
        public async Task OpenMarkdownFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "打开 Markdown 文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Markdown 文件", Extensions = { "md", "markdown", "txt" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = { "*" } }
                }
            };
            
            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                var filePath = result[0];
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.OpenFile(filePath);
                }
            }
        }
        
        /// <summary>
        /// 处理打开文件菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OpenFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await OpenMarkdownFileAsync();
        }
        
        /// <summary>
        /// 保存 Markdown 文件
        /// </summary>
        /// <returns></returns>
        public async Task SaveMarkdownFileAsync()
        {
            if (DataContext is MainWindowViewModel vm && !string.IsNullOrEmpty(vm.CurrentFilePath))
            {
                vm.SaveFile(vm.CurrentFilePath);
            }
            else
            {
                await SaveMarkdownFileAsAsync();
            }
        }
        
        /// <summary>
        /// 另存为 Markdown 文件
        /// </summary>
        /// <returns></returns>
        public async Task SaveMarkdownFileAsAsync()
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存 Markdown 文件",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Markdown 文件", Extensions = { "md", "markdown" } },
                    new FileDialogFilter { Name = "文本文件", Extensions = { "txt" } },
                    new FileDialogFilter { Name = "所有文件", Extensions = { "*" } }
                }
            };
            
            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.SaveFile(result);
                }
            }
        }
        
        /// <summary>
        /// 处理保存文件菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void SaveFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsync();
        }
        
        /// <summary>
        /// 处理另存为菜单项的点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void SaveAsFile_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveMarkdownFileAsAsync();
        }
        
        // 工具栏按钮事件处理程序
        
        /// <summary>
        /// 处理粗体按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BoldButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("**", "**");
        }
        
        /// <summary>
        /// 处理斜体按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ItalicButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("*", "*");
        }
        
        /// <summary>
        /// 处理下划线按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void UnderlineButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("<u>", "</u>");
        }
        
        /// <summary>
        /// 处理一级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H1Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("# ", "");
        }
        
        /// <summary>
        /// 处理二级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H2Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("## ", "");
        }
        
        /// <summary>
        /// 处理三级标题按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void H3Button_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("### ", "");
        }
        
        /// <summary>
        /// 处理无序列表按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void BulletListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("- ", "");
        }
        
        /// <summary>
        /// 处理有序列表按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void NumberedListButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("1. ", "");
        }
        
        /// <summary>
        /// 处理代码块按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void CodeBlockButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("```\n", "\n```");
        }
        
        /// <summary>
        /// 处理链接按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void LinkButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("[链接文本](", ")");
        }
        
        /// <summary>
        /// 处理图片按钮点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void ImageButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InsertMarkdownSyntax("![图片描述](", ")");
        }
        
        /// <summary>
        /// 在编辑器中插入 Markdown 语法
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <param name="suffix">后缀</param>
        private void InsertMarkdownSyntax(string prefix, string suffix)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // 简单实现：在编辑器内容末尾添加语法
                vm.EditorContent += prefix + suffix;
            }
        }
    }
}