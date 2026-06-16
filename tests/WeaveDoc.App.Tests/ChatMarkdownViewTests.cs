using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WeaveDoc.App.Views;
using Xunit;

namespace WeaveDoc.App.Tests;

public sealed class ChatMarkdownViewTests
{
    [AvaloniaFact]
    public async Task RendersHeadingsListsEmphasisAndInlineCode()
    {
        var view = new ChatMarkdownView
        {
            Markdown = """
                       ## 标题

                       这是一段 **重点** 和 `code`。

                       - 第一项
                       - 第二项
                       """
        };

        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var text = CollectText(view);
            Assert.Contains("标题", text);
            Assert.Contains("这是一段", text);
            Assert.Contains("重点", text);
            Assert.Contains("code", text);
            Assert.Contains("第一项", text);
            Assert.Contains("第二项", text);

            var runs = CollectRuns(view).ToList();
            Assert.Contains(runs, run => run.Text == "重点" && run.FontWeight == Avalonia.Media.FontWeight.SemiBold);
            Assert.Contains(runs, run => run.Text == "code" && run.FontFamily is not null);
        });
    }

    [AvaloniaFact]
    public async Task RendersFencedCodeBlockWithCopyButtonAndPreservedNewlines()
    {
        var view = new ChatMarkdownView
        {
            Markdown = """
                       ```csharp
                       var x = 1;
                       return x;
                       ```
                       """
        };

        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var codeTextBox = Find<TextBox>(view, "MarkdownCodeBlockTextBox");
            var copyButton = Find<Button>(view, "MarkdownCodeCopyButton");

            Assert.Contains("var x = 1;\nreturn x;", codeTextBox.Text);
            Assert.Equal("复制", copyButton.Content);
        });
    }

    [AvaloniaFact]
    public async Task EmptyMarkdownRendersNoContent()
    {
        var view = new ChatMarkdownView { Markdown = "" };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            Assert.Empty(view.GetVisualDescendants().OfType<TextBlock>());
            Assert.Empty(view.GetVisualDescendants().OfType<TextBox>());
        });
    }

    [AvaloniaFact]
    public async Task ExtractsCitationFromBodyAndShowsSuperscriptMarker()
    {
        var view = new ChatMarkdownView
        {
            Markdown = "控制模块负责状态显示。[doc/a.md | 方法设计 | c3]"
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var text = CollectText(view);
            Assert.Contains("控制模块负责状态显示。", text);
            Assert.DoesNotContain("[doc/a.md | 方法设计 | c3]", text);

            Assert.Contains(CollectRuns(view), run =>
                run.Text == "1" && run.BaselineAlignment == Avalonia.Media.BaselineAlignment.Superscript);
            Assert.Equal("来源 1 ▼", Find<Button>(view, "MarkdownSourcesToggleButton").Content);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Border>(), border => border.Name == "MarkdownSourceRow");
        });
    }

    [AvaloniaFact]
    public async Task ExpandsSourcesWithDeduplicatedRowsInFirstSeenOrder()
    {
        var view = new ChatMarkdownView
        {
            Markdown = """
                       第一段。[doc/a.md | 方法设计 | c3]
                       第二段。[doc/b.md | 结果分析 | c7]
                       第三段复用来源。[doc/a.md | 方法设计 | c3]
                       """
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            Click(Find<Button>(view, "MarkdownSourcesToggleButton"));
            Arrange(host);

            Assert.Equal("来源 2 ▲", Find<Button>(view, "MarkdownSourcesToggleButton").Content);
            var rows = view.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Name == "MarkdownSourceRow")
                .ToList();
            Assert.Equal(2, rows.Count);

            var text = CollectText(view);
            Assert.Contains("a.md", text);
            Assert.Contains("方法设计 · c3", text);
            Assert.Contains("b.md", text);
            Assert.Contains("结果分析 · c7", text);
        });
    }

    [AvaloniaFact]
    public async Task ExtractsCompactThreePartCitationFromBody()
    {
        var view = new ChatMarkdownView
        {
            Markdown = "控制模块负责状态显示。[doc/a.md|方法设计|c3]"
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var text = CollectText(view);
            Assert.Contains("控制模块负责状态显示。", text);
            Assert.DoesNotContain("[doc/a.md|方法设计|c3]", text);
            Assert.Contains(CollectRuns(view), run =>
                run.Text == "1" && run.BaselineAlignment == Avalonia.Media.BaselineAlignment.Superscript);

            Click(Find<Button>(view, "MarkdownSourcesToggleButton"));
            Arrange(host);

            text = CollectText(view);
            Assert.Contains("a.md", text);
            Assert.Contains("方法设计 · c3", text);
        });
    }

    [AvaloniaFact]
    public async Task ExtractsTwoPartCitationAndDefaultsSectionToBody()
    {
        var view = new ChatMarkdownView
        {
            Markdown = "控制模块负责状态显示。[doc/a.md|c3]"
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var text = CollectText(view);
            Assert.Contains("控制模块负责状态显示。", text);
            Assert.DoesNotContain("[doc/a.md|c3]", text);
            Assert.Contains(CollectRuns(view), run =>
                run.Text == "1" && run.BaselineAlignment == Avalonia.Media.BaselineAlignment.Superscript);

            Click(Find<Button>(view, "MarkdownSourcesToggleButton"));
            Arrange(host);

            text = CollectText(view);
            Assert.Contains("a.md", text);
            Assert.Contains("正文 · c3", text);
        });
    }

    [AvaloniaFact]
    public async Task ExtractsBareChunkCitationWithoutLeakingMarkerText()
    {
        var view = new ChatMarkdownView
        {
            Markdown = "控制模块负责状态显示 [c3]。"
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var text = CollectText(view);
            Assert.Contains("控制模块负责状态显示", text);
            Assert.DoesNotContain("[c3]", text);
            Assert.Contains(CollectRuns(view), run =>
                run.Text == "1" && run.BaselineAlignment == Avalonia.Media.BaselineAlignment.Superscript);

            Click(Find<Button>(view, "MarkdownSourcesToggleButton"));
            Arrange(host);

            text = CollectText(view);
            Assert.Contains("当前检索上下文", text);
            Assert.Contains("正文 · c3", text);
        });
    }

    [AvaloniaFact]
    public async Task DeduplicatesEquivalentCitationSpacingVariants()
    {
        var view = new ChatMarkdownView
        {
            Markdown = """
                       第一段。[doc/a.md | 方法设计 | c3]
                       第二段复用紧凑格式。[doc/a.md|方法设计|c3]
                       """
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            Click(Find<Button>(view, "MarkdownSourcesToggleButton"));
            Arrange(host);

            Assert.Equal("来源 1 ▲", Find<Button>(view, "MarkdownSourcesToggleButton").Content);
            var rows = view.GetVisualDescendants().OfType<Border>()
                .Where(border => border.Name == "MarkdownSourceRow")
                .ToList();
            Assert.Single(rows);

            var superscripts = CollectRuns(view)
                .Where(run => run.BaselineAlignment == Avalonia.Media.BaselineAlignment.Superscript)
                .Select(run => run.Text)
                .ToList();
            Assert.Equal(["1", "1"], superscripts);
        });
    }

    [AvaloniaFact]
    public async Task KeepsCitationLikeTextInsideCodeBlock()
    {
        var view = new ChatMarkdownView
        {
            Markdown = """
                       ```text
                       [doc/a.md | 方法设计 | c3]
                       [doc/a.md|c3]
                       [c3]
                       ```
                       """
        };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            var codeTextBox = Find<TextBox>(view, "MarkdownCodeBlockTextBox");
            Assert.Contains("[doc/a.md | 方法设计 | c3]", codeTextBox.Text);
            Assert.Contains("[doc/a.md|c3]", codeTextBox.Text);
            Assert.Contains("[c3]", codeTextBox.Text);
            Assert.Null(view.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => button.Name == "MarkdownSourcesToggleButton"));
        });
    }

    [AvaloniaFact]
    public async Task MarkdownWithoutCitationsDoesNotRenderSourcesSection()
    {
        var view = new ChatMarkdownView { Markdown = "没有来源标签的普通回答。" };
        var host = ShowInHost(view);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Arrange(host);

            Assert.Null(view.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => button.Name == "MarkdownSourcesToggleButton"));
        });
    }

    private static Window ShowInHost(Control content)
    {
        var host = new Window
        {
            Width = 640,
            Height = 480,
            Content = content
        };
        host.Show();
        return host;
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        var control = root.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
        Assert.NotNull(control);
        return control!;
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    private static void Arrange(Window window)
    {
        window.Measure(new Avalonia.Size(window.Width, window.Height));
        window.Arrange(new Avalonia.Rect(0, 0, window.Width, window.Height));
    }

    private static string CollectText(Control root)
    {
        var values = root.GetVisualDescendants()
            .Select(control => control switch
            {
                TextBlock textBlock => !string.IsNullOrEmpty(textBlock.Text)
                    ? textBlock.Text
                    : string.Join("", textBlock.Inlines?.Select(inline => (inline as Run)?.Text ?? string.Empty) ?? []),
                TextBox textBox => textBox.Text,
                Button button => button.Content?.ToString(),
                _ => null
            })
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join('\n', values);
    }

    private static IEnumerable<Run> CollectRuns(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<TextBlock>()
            .SelectMany(textBlock => textBlock.Inlines?.OfType<Run>() ?? []);
    }
}
