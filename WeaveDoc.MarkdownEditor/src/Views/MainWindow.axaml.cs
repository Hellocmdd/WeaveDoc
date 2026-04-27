using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log("MainWindow: Constructor called");
            InitializeComponent();
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log("MainWindow: InitializeComponent completed");
            // 将 MainWindowViewModel 设置为 DataContext
            var vm = new MainWindowViewModel();
            DataContext = vm;
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log("MainWindow: DataContext set to MainWindowViewModel");

            // 移除对 PreviewTextBlock, DebugRawHtml, DebugInfo 的引用，因为它们在 XAML 中不存在或已不再需要
            // ViewModel 中的 Html 属性更新会自动触发 MonacoEditorControl 或其他绑定控件的更新（如果已正确绑定）
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log("MainWindow: OnOpened called");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            WeaveDoc.MarkdownEditor.Helpers.Logger.Log("MainWindow: OnClosed called");
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}