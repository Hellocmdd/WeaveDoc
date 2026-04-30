using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WeaveDoc.MarkdownEditor.Helpers;

namespace WeaveDoc.MarkdownEditor.Controls
{
    public partial class PreviewWebViewControl : UserControl
    {
        private TextBox? _previewContent;

        public PreviewWebViewControl()
        {
            InitializeComponent();
            _previewContent = this.FindControl<TextBox>("PreviewContent");
            Logger.Log($"PreviewWebViewControl: Constructor - PreviewContent: {_previewContent != null}");
        }

        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<PreviewWebViewControl, string>(
                nameof(HtmlContent),
                defaultBindingMode: BindingMode.OneWay);

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HtmlContentProperty)
            {
                var newContent = change.NewValue as string;
                Logger.Log($"PreviewWebViewControl: HtmlContent property changed, length: {newContent?.Length ?? 0}");
                UpdatePreview(newContent ?? string.Empty);
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Logger.Log("PreviewWebViewControl: OnLoaded called");
            
            // 显示初始预览内容
            if (_previewContent != null)
            {
                _previewContent.Text = "Welcome to WeaveDoc Preview\n\nStart typing in the editor to see the preview!";
            }
        }

        private void UpdatePreview(string content)
        {
            if (_previewContent != null)
            {
                // 显示内容（去除 HTML 标签）
                var plainText = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]*>", string.Empty);
                _previewContent.Text = plainText;
                Logger.Log("PreviewWebViewControl: Preview updated");
            }
        }

        public void SetContent(string content)
        {
            HtmlContent = content;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
