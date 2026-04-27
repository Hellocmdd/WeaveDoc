using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;
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
                Filters = new[]
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
    }
}