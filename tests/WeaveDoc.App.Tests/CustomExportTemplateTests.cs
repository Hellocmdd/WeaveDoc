using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.App.Services.ExportTemplates;
using WeaveDoc.App.Views;
using WeaveDoc.Converter.Afd;
using Xunit;

namespace WeaveDoc.App.Tests;

public class CustomExportTemplateTests
{
    [Fact]
    public void CustomTemplateBuilder_CreatesValidAfdTemplateFromPresetOptions()
    {
        var template = CustomExportTemplateBuilder.Create(new CustomExportTemplateOptions
        {
            TemplateName = "我的论文模板",
            Description = "用于毕业论文导出",
            BaseFontFamily = "微软雅黑",
            BaseFontSize = 12,
            LineSpacing = 1.5,
            MarginPreset = TemplateMarginPreset.Thesis,
            PagePreset = TemplatePagePreset.A4,
            FirstLineIndentPreset = TemplateFirstLineIndentPreset.TwoCharacters,
            HeadingPreset = TemplateHeadingPreset.Academic,
            CodeFontFamily = "Consolas",
            CodeFontSize = 10
        });

        new AfdParser().Validate(template);

        Assert.Equal("我的论文模板", template.Meta.TemplateName);
        Assert.Equal("微软雅黑", template.Defaults.FontFamily);
        Assert.Equal(12, template.Defaults.FontSize);
        Assert.Equal(1.5, template.Defaults.LineSpacing);
        Assert.Equal(210, template.Defaults.PageSize?.Width);
        Assert.Equal(297, template.Defaults.PageSize?.Height);
        Assert.Equal(30, template.Defaults.Margins?.Left);
        Assert.Equal(30, template.Defaults.Margins?.Right);
        Assert.Equal(24, template.Styles["body"].FirstLineIndent);
        Assert.Equal("黑体", template.Styles["heading1"].FontFamily);
        Assert.True(template.Styles["heading1"].Bold);
        Assert.Equal("center", template.Styles["heading1"].Alignment);
        Assert.Equal("Consolas", template.Styles["codeblock"].FontFamily);
    }

    [Fact]
    public void CustomTemplateBuilder_ExposesControlledOptionLists()
    {
        Assert.Contains("宋体", CustomExportTemplateOptionsCatalog.FontFamilies);
        Assert.Contains("微软雅黑", CustomExportTemplateOptionsCatalog.FontFamilies);
        Assert.Contains(10.5, CustomExportTemplateOptionsCatalog.FontSizes);
        Assert.Contains(1.5, CustomExportTemplateOptionsCatalog.LineSpacings);
        Assert.Contains(TemplateMarginPreset.Thesis, CustomExportTemplateOptionsCatalog.MarginPresets);
        Assert.Contains(TemplateHeadingPreset.Academic, CustomExportTemplateOptionsCatalog.HeadingPresets);
    }

    [AvaloniaFact]
    public async Task SettingsDialog_TemplateLibrary_ShowsCustomTemplateEditorControls()
    {
        var dialog = new SettingsDialog(null, null, SettingsDialogTab.Template);
        dialog.Show();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var createButton = dialog.FindControl<Button>("CreateCustomTemplateButton");
            Assert.NotNull(createButton);

            createButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.True(dialog.FindControl<Border>("CustomTemplateEditorPanel")?.IsVisible);

            var fontBox = dialog.FindControl<ComboBox>("TemplateBaseFontComboBox");
            var sizeBox = dialog.FindControl<ComboBox>("TemplateBaseFontSizeComboBox");
            var spacingBox = dialog.FindControl<ComboBox>("TemplateLineSpacingComboBox");

            Assert.NotNull(fontBox);
            Assert.NotNull(sizeBox);
            Assert.NotNull(spacingBox);
            Assert.NotEmpty(GetItemsSource(fontBox!));
            Assert.NotEmpty(GetItemsSource(sizeBox!));
            Assert.NotEmpty(GetItemsSource(spacingBox!));
        });
    }

    private static IEnumerable<object> GetItemsSource(ComboBox comboBox) =>
        (comboBox.ItemsSource as IEnumerable)?.Cast<object>() ?? [];
}
