using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WeaveDoc.MarkdownEditor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // 将 MainWindowViewModel 设置为 DataContext，并用代码在运行时绑定控件到 ViewModel
            var vm = new ViewModels.MainWindowViewModel();
            DataContext = vm;

            // 当 ViewModel 的 Html 变更时，更新右侧预览控件文本
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.Html))
                {
                    // 记录 HTML 内容以便调试（日志会放在项目根目录）
                    try { Helpers.Logger.Log($"Preview HTML updated: {vm.Html}"); } catch { }

                    // PreviewTextBlock 在 XAML 中声明了 x:Name，但在极少数情况下（初始化顺序）可能为 null，先做空检查
                    if (PreviewTextBlock != null)
                    {
                        // 为了让预览更直观，先展示去除标签并解码后的纯文本版本；后续可切换为 WebView 来渲染 HTML
                        try
                        {
                                var html = vm.Html ?? string.Empty;
                                var decoded = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty));
                                // 记录解码后的预览内容与长度，便于调试
                                try { Helpers.Logger.Log($"Decoded preview (len={decoded?.Length ?? 0}): {decoded}"); } catch { }
                                PreviewTextBlock.Text = decoded;

                                // 同步写入调试面板（原始 HTML 与信息）以便用户观察
                                try
                                {
                                    DebugRawHtml.Text = html;
                                    DebugInfo.Text = $"Decoded length: {(decoded?.Length ?? 0)}, Raw length: {html.Length}";
                                }
                                catch { }
                        }
                        catch
                        {
                            // 回退到原始 HTML 文本
                            PreviewTextBlock.Text = vm.Html;
                        }
                    }
                }
            };
        }

        // Deleted:private void Editor_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        // Deleted:{
        // Deleted:    try
        // Deleted:    {
        // Deleted:        if (DataContext is ViewModels.MainWindowViewModel vm && sender is Avalonia.Controls.TextBox tb)
        // Deleted:        {
        // Deleted:            // 直接将文本内容写回 ViewModel，保证实时预览
        // Deleted:            vm.EditorContent = tb.Text ?? string.Empty;
        // Deleted:        }
        // Deleted:    }
        // Deleted:    catch (Exception ex)
        // Deleted:    {
        // Deleted:        // 记录异常，避免应用直接崩溃，并友好提示
        // Deleted:        WeaveDoc.MarkdownEditor.Helpers.Logger.LogException(ex);
        // Deleted:        try
        // Deleted:        {
        // Deleted:            // 尝试弹出一个基础窗口提示用户（不抛出）
        // Deleted:            var dialog = new Avalonia.Controls.Window
        // Deleted:            {
        // Deleted:                Title = "错误",
        // Deleted:                Width = 400,
        // Deleted:                Height = 120,
        // Deleted:                Content = new Avalonia.Controls.TextBlock { Text = "发生错误，已记录到日志。请查看日志文件。", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(12) }
        // Deleted:            };
        // Deleted:            dialog.Show();
        // Deleted:        }
        // Deleted:        catch { }
        // Deleted:    }
        // Deleted:}

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}