using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 将 MainWindowViewModel 设置为 DataContext
            var vm = new ViewModels.MainWindowViewModel();
            DataContext = vm;

            // 移除对 PreviewTextBlock, DebugRawHtml, DebugInfo 的引用，因为它们在 XAML 中不存在或已不再需要
            // ViewModel 中的 Html 属性更新会自动触发 MonacoEditorControl 或其他绑定控件的更新（如果已正确绑定）
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}