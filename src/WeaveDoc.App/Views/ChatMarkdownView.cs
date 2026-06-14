using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace WeaveDoc.App.Views;

public sealed class ChatMarkdownView : UserControl
{
    private const string CitationMarkerPrefix = "\uE000WD_CITATION_";
    private const string CitationMarkerSuffix = "\uE001";

    private static readonly Regex StableCitationRegex = new(
        @"\[(?<file>[^\]\r\n|]+)\s*\|\s*(?<section>[^\]\r\n|]+)\s*\|\s*(?<chunk>c\d+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FencedCodeBlockRegex = new(
        @"(?ms)^```.*?^```[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .Build();

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<ChatMarkdownView, string>(nameof(Markdown), string.Empty);

    private readonly StackPanel _root = new()
    {
        Spacing = 6,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private readonly List<CitationSource> _sources = [];
    private bool _sourcesExpanded;

    public ChatMarkdownView()
    {
        Content = _root;
        RenderMarkdown();
    }

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value ?? string.Empty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
        {
            RenderMarkdown();
        }
    }

    private void RenderMarkdown()
    {
        _root.Children.Clear();

        if (string.IsNullOrWhiteSpace(Markdown))
        {
            return;
        }

        var presentation = BuildPresentation(Markdown);
        _sources.Clear();
        _sources.AddRange(presentation.Sources);

        var document = Markdig.Markdown.Parse(presentation.Markdown, Pipeline);
        foreach (var block in document)
        {
            var control = RenderBlock(block);
            if (control is not null)
            {
                _root.Children.Add(control);
            }
        }

        if (_sources.Count > 0)
        {
            _root.Children.Add(RenderSourcesSection());
        }
    }

    private static MarkdownPresentation BuildPresentation(string markdown)
    {
        var protectedBlocks = new List<string>();
        var protectedMarkdown = FencedCodeBlockRegex.Replace(markdown, match =>
        {
            var token = $"@@WEAVEDOC_CODE_BLOCK_{protectedBlocks.Count}@@";
            protectedBlocks.Add(match.Value);
            return token;
        });

        var sources = new List<CitationSource>();
        var sourceIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var body = StableCitationRegex.Replace(protectedMarkdown, match =>
        {
            var fullCitation = match.Value;
            if (!sourceIndex.TryGetValue(fullCitation, out var index))
            {
                index = sources.Count + 1;
                sourceIndex[fullCitation] = index;
                sources.Add(new CitationSource(
                    index,
                    fullCitation,
                    match.Groups["file"].Value.Trim(),
                    match.Groups["section"].Value.Trim(),
                    match.Groups["chunk"].Value.Trim()));
            }

            return $" {CitationMarkerPrefix}{index}{CitationMarkerSuffix}";
        });

        for (var i = 0; i < protectedBlocks.Count; i++)
        {
            body = body.Replace($"@@WEAVEDOC_CODE_BLOCK_{i}@@", protectedBlocks[i], StringComparison.Ordinal);
        }

        return new MarkdownPresentation(body, sources);
    }

    private Control? RenderBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            CodeBlock codeBlock => RenderCodeBlock(codeBlock),
            QuoteBlock quote => RenderQuote(quote),
            ListBlock list => RenderList(list),
            Table table => RenderTable(table),
            ThematicBreakBlock => RenderDivider(),
            HtmlBlock htmlBlock => RenderPlainBlock(htmlBlock.Lines.ToString()),
            ContainerBlock container => RenderContainer(container),
            _ => null
        };
    }

    private TextBlock RenderHeading(HeadingBlock heading)
    {
        var textBlock = CreateTextBlock(fontSize: heading.Level <= 2 ? 15 : 13, fontWeight: FontWeight.SemiBold);
        textBlock.Margin = new Thickness(0, heading.Level <= 2 ? 2 : 1, 0, 0);
        AddInlines(textBlock.Inlines!, heading.Inline);
        return textBlock;
    }

    private TextBlock RenderParagraph(ParagraphBlock paragraph)
    {
        var textBlock = CreateTextBlock();
        AddInlines(textBlock.Inlines!, paragraph.Inline);
        return textBlock;
    }

    private Control RenderCodeBlock(CodeBlock codeBlock)
    {
        var code = ExtractCode(codeBlock);
        var language = codeBlock is FencedCodeBlock fenced ? fenced.Info?.Trim() ?? string.Empty : string.Empty;

        var codeText = new TextBox
        {
            Name = "MarkdownCodeBlockTextBox",
            Text = code,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("JetBrains Mono, Cascadia Code, Consolas, monospace"),
            FontSize = 11,
            MinHeight = 34,
            MaxHeight = 220,
            Padding = new Thickness(10, 8),
            Background = Brush("ShellInputBrush"),
            Foreground = Brush("ShellTextBrush"),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(codeText, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(codeText, ScrollBarVisibility.Auto);

        var copyButton = new Button
        {
            Name = "MarkdownCodeCopyButton",
            Classes = { "command-button" },
            Content = "复制",
            MinWidth = 0,
            Height = 26,
            Padding = new Thickness(10, 1, 10, 0),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        copyButton.Click += async (_, _) =>
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(code);
                }
            }
            catch
            {
                // Copy is a convenience action; rendering should never fail because the clipboard is unavailable.
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = Brush("ShellChromeBrush"),
        };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language) ? "code" : language,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("ShellMutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 8, 0),
        });
        Grid.SetColumn(copyButton, 1);
        header.Children.Add(copyButton);

        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("30,*"),
            MinWidth = 0,
        };
        panel.Children.Add(header);
        Grid.SetRow(codeText, 1);
        panel.Children.Add(codeText);

        return new Border
        {
            Name = "MarkdownCodeBlock",
            Background = Brush("ShellInputBrush"),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = Radius("ShellRadiusMd", 7),
            ClipToBounds = true,
            Child = panel,
        };
    }

    private Control RenderQuote(QuoteBlock quote)
    {
        var inner = RenderContainer(quote);
        return new Border
        {
            Background = Brush("ShellInputBrush"),
            BorderBrush = Brush("ShellAccentBrush"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = Radius("ShellRadiusSm", 4),
            Padding = new Thickness(10, 6),
            Child = inner,
        };
    }

    private Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 };
        var index = int.TryParse(list.OrderedStart, out var orderedStart) && orderedStart > 0
            ? orderedStart
            : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 7,
            };
            row.Children.Add(new TextBlock
            {
                Text = list.IsOrdered ? $"{index}." : "•",
                FontSize = 12,
                Foreground = Brush("ShellMutedTextBrush"),
                VerticalAlignment = VerticalAlignment.Top,
            });

            var itemContent = RenderContainer(item);
            Grid.SetColumn(itemContent, 1);
            row.Children.Add(itemContent);
            panel.Children.Add(row);
            index++;
        }

        return panel;
    }

    private Control RenderTable(Table table)
    {
        var grid = new Grid
        {
            Name = "MarkdownTable",
            RowDefinitions = new RowDefinitions(),
            ColumnDefinitions = new ColumnDefinitions(),
            MinWidth = 0,
        };

        var rows = table.OfType<TableRow>().ToList();
        var columnCount = Math.Max(1, rows.Select(row => row.Count).DefaultIfEmpty(1).Max());
        for (var i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (row[columnIndex] is not TableCell cell)
                {
                    continue;
                }

                var cellContent = RenderTableCell(cell, row.IsHeader);
                Grid.SetRow(cellContent, rowIndex);
                Grid.SetColumn(cellContent, columnIndex);
                grid.Children.Add(cellContent);
            }
        }

        return new Border
        {
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = Radius("ShellRadiusSm", 4),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = grid,
            }
        };
    }

    private Control RenderTableCell(TableCell cell, bool isHeader)
    {
        var content = RenderContainer(cell);
        return new Border
        {
            Background = isHeader ? Brush("ShellChromeBrush") : Brush("ShellCardBrush"),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(8, 5),
            Child = content,
        };
    }

    private static Border RenderDivider()
    {
        return new Border
        {
            Height = 1,
            Margin = new Thickness(0, 3),
            Background = Brush("ShellBorderBrush"),
        };
    }

    private TextBlock RenderPlainBlock(string text)
    {
        var textBlock = CreateTextBlock();
        textBlock.Text = text.TrimEnd();
        return textBlock;
    }

    private StackPanel RenderContainer(ContainerBlock container)
    {
        var panel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var child in container)
        {
            var rendered = RenderBlock(child);
            if (rendered is not null)
            {
                panel.Children.Add(rendered);
            }
        }

        return panel;
    }

    private static TextBlock CreateTextBlock(double fontSize = 12, FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeight.Normal,
            Foreground = Brush("ShellTextBrush"),
            LineHeight = fontSize + 6,
        };
    }

    private void AddInlines(InlineCollection inlines, ContainerInline? container, InlineStyle style = default)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AddInline(inlines, inline, style);
        }
    }

    private void AddInline(InlineCollection inlines, Markdig.Syntax.Inlines.Inline inline, InlineStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AddLiteralRuns(inlines, literal.Content.ToString(), style);
                break;
            case CodeInline code:
                AddRun(inlines, code.Content, style with { IsCode = true });
                break;
            case EmphasisInline emphasis:
                var nextStyle = style with
                {
                    IsBold = style.IsBold || emphasis.DelimiterCount >= 2,
                    IsItalic = style.IsItalic || emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3,
                    IsStrike = style.IsStrike || emphasis.DelimiterChar == '~'
                };
                AddInlines(inlines, emphasis, nextStyle);
                break;
            case LinkInline link:
                if (link.IsImage)
                {
                    AddRun(inlines, link.Url ?? string.Empty, style with { IsLink = true });
                }
                else
                {
                    AddInlines(inlines, link, style with { IsLink = true });
                }
                break;
            case LineBreakInline:
                inlines.Add(new LineBreak());
                break;
            case HtmlInline html:
                AddRun(inlines, html.Tag, style);
                break;
            case ContainerInline childContainer:
                AddInlines(inlines, childContainer, style);
                break;
        }
    }

    private static void AddLiteralRuns(InlineCollection inlines, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var cursor = 0;
        var pattern = Regex.Escape(CitationMarkerPrefix) + @"(\d+)" + Regex.Escape(CitationMarkerSuffix);
        foreach (Match match in Regex.Matches(text, pattern))
        {
            if (match.Index > cursor)
            {
                AddRun(inlines, text[cursor..match.Index], style);
            }

            AddCitationRun(inlines, match.Groups[1].Value);
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            AddRun(inlines, text[cursor..], style);
        }
    }

    private static void AddCitationRun(InlineCollection inlines, string number)
    {
        inlines.Add(new Run(number)
        {
            FontSize = 9,
            BaselineAlignment = BaselineAlignment.Superscript,
            Foreground = Brush("ShellAccentBrush"),
            FontWeight = FontWeight.SemiBold,
        });
    }

    private static void AddRun(InlineCollection inlines, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var run = new Run(text)
        {
            FontWeight = style.IsBold ? FontWeight.SemiBold : FontWeight.Normal,
            FontStyle = style.IsItalic ? FontStyle.Italic : FontStyle.Normal,
            TextDecorations = style.IsStrike
                ? TextDecorations.Strikethrough
                : style.IsLink
                    ? TextDecorations.Underline
                    : null,
            Foreground = style.IsLink ? Brush("ShellAccentBrush") : Brush("ShellTextBrush"),
        };

        if (style.IsCode)
        {
            run.FontFamily = new FontFamily("JetBrains Mono, Cascadia Code, Consolas, monospace");
            run.FontSize = 11;
            run.Background = Brush("ShellInputBrush");
        }

        inlines.Add(run);
    }

    private static string ExtractCode(CodeBlock codeBlock)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < codeBlock.Lines.Count; i++)
        {
            builder.Append(codeBlock.Lines.Lines[i].Slice);
            if (i < codeBlock.Lines.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private Control RenderSourcesSection()
    {
        var panel = new StackPanel
        {
            Name = "MarkdownSourcesSection",
            Spacing = 6,
            Margin = new Thickness(0, 4, 0, 0),
        };

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = Brush("ShellBorderBrush"),
            Opacity = 0.8,
        });

        var toggleButton = new Button
        {
            Name = "MarkdownSourcesToggleButton",
            Classes = { "command-button" },
            Content = _sourcesExpanded ? $"来源 {_sources.Count} ▲" : $"来源 {_sources.Count} ▼",
            Height = 26,
            MinWidth = 0,
            Padding = new Thickness(10, 1, 10, 0),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        toggleButton.Click += (_, _) =>
        {
            _sourcesExpanded = !_sourcesExpanded;
            RenderMarkdown();
        };
        panel.Children.Add(toggleButton);

        if (_sourcesExpanded)
        {
            var list = new StackPanel
            {
                Name = "MarkdownSourcesList",
                Spacing = 5,
            };
            foreach (var source in _sources)
            {
                list.Children.Add(RenderSourceRow(source));
            }

            panel.Children.Add(list);
        }

        return panel;
    }

    private Control RenderSourceRow(CitationSource source)
    {
        var copyButton = new Button
        {
            Name = "MarkdownSourceCopyButton",
            Classes = { "command-button" },
            Content = "复制",
            Height = 24,
            MinWidth = 0,
            Padding = new Thickness(8, 1, 8, 0),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        copyButton.Click += async (_, _) =>
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                {
                    await clipboard.SetTextAsync(source.FullCitation);
                }
            }
            catch
            {
                // Same as code-block copy: source copy is optional and must not disrupt rendering.
            }
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 7,
        };

        row.Children.Add(new Border
        {
            Background = Brush("ShellSelectedBrush"),
            CornerRadius = Radius("ShellRadiusSm", 4),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = source.Number.ToString(),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush("ShellTextBrush"),
            }
        });

        var details = new StackPanel { Spacing = 1 };
        details.Children.Add(new TextBlock
        {
            Name = "MarkdownSourceFileText",
            Text = FormatSourceFile(source.FilePath),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("ShellTextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        details.Children.Add(new TextBlock
        {
            Name = "MarkdownSourceDetailText",
            Text = $"{source.Section} · {source.ChunkId}",
            FontSize = 10,
            Foreground = Brush("ShellMutedTextBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(details, 1);
        row.Children.Add(details);

        Grid.SetColumn(copyButton, 2);
        row.Children.Add(copyButton);

        return new Border
        {
            Name = "MarkdownSourceRow",
            Background = Brush("ShellInputBrush"),
            BorderBrush = Brush("ShellBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = Radius("ShellRadiusMd", 7),
            Padding = new Thickness(7, 5),
            Child = row,
        };
    }

    private static string FormatSourceFile(string filePath)
    {
        var normalized = filePath.Replace('\\', '/').Trim();
        var fileName = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(fileName) ? normalized : fileName;
    }

    private static IBrush? Brush(string key)
    {
        return Application.Current?.Resources[key] as IBrush;
    }

    private static CornerRadius Radius(string key, double fallback)
    {
        return Application.Current?.Resources[key] as CornerRadius? ?? new CornerRadius(fallback);
    }

    private sealed record MarkdownPresentation(string Markdown, IReadOnlyList<CitationSource> Sources);

    private sealed record CitationSource(int Number, string FullCitation, string FilePath, string Section, string ChunkId);

    private readonly record struct InlineStyle(bool IsBold, bool IsItalic, bool IsCode, bool IsLink, bool IsStrike);
}
