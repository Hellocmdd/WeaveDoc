using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.ViewModels;

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
    }
}