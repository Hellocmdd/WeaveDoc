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
        private TextBlock? _previewContent;

        public PreviewWebViewControl()
        {
            InitializeComponent();
            _previewContent = this.FindControl<TextBlock>("PreviewContent");
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
                UpdatePreview(newContent ?? string.Empty);
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            if (_previewContent != null)
            {
                _previewContent.Text = "Welcome to WeaveDoc Preview\n\nStart typing in the editor to see the preview!";
            }
        }

        private void UpdatePreview(string content)
        {
            if (_previewContent != null)
            {
                var plainText = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]*>", string.Empty);
                _previewContent.Text = plainText;
            }
        }

        public void SetContent(string content)
        {
            HtmlContent = content;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
