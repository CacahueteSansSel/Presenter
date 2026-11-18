using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Presenter.Documents;

namespace Presenter.Views;

internal static partial class SlideText
{
    private static readonly IBrush CodeInk = Ui.Brush("#E0EFE8");

    public static Control MarkdownText(SlideElement element, string content)
    {
        StackPanel panel = new() { Spacing = element.FontSize * .18 };
        AppendBlocks(panel, Markdown.Parse(content), element);

        return panel;
    }

    public static Control RenderCode(SlideElement element, string content)
    {
        TextBlock text = new()
        {
            FontFamily = FontCatalog.Resolve(element.Font),
            FontSize = element.FontSize,
            FontWeight = element.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = element.Italic ? FontStyle.Italic : FontStyle.Normal,
            LineHeight = element.FontSize * 1.52,
            Foreground = CodeInk,
            TextWrapping = TextWrapping.Wrap
        };

        int offset = 0;
        InlineCollection inlines = text.Inlines ?? [];
        foreach (Match match in CodeTokens().Matches(content))
        {
            if (match.Index > offset)
                inlines.Add(new Run(content[offset..match.Index]));

            string token = match.Value;
            IBrush color = CodeInk;
            if (match.Groups["comment"].Success)
                color = Ui.Brush("#86ABA0");
            else if (match.Groups["string"].Success)
                color = Ui.Brush("#EAC58D");
            else if (match.Groups["keyword"].Success)
                color = Ui.Brush("#C7BAFF");
            else if (match.Groups["number"].Success)
                color = Ui.Brush("#81D8C2");
            else if (match.Groups["call"].Success)
                color = Ui.Brush("#92E2BE");

            inlines.Add(new Run(token) { Foreground = color });
            offset = match.Index + match.Length;
        }

        if (offset < content.Length)
            inlines.Add(new Run(content[offset..]));

        Border body = new()
        {
            Padding = new Thickness(25, 18),
            Child = text,
            ClipToBounds = true
        };

        Grid frame = new() { RowDefinitions = new RowDefinitions(element.Kind == ElementKind.Window ? "50,*" : "*") };
        if (element.Kind == ElementKind.Window)
        {
            Control caption = new TextBlock
            {
                Text = element.Caption,
                FontFamily = Ui.Mono,
                FontSize = 18,
                Foreground = Ui.Brush("#A8C4B9"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            StackPanel header = new()
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                Children = { Ui.Icon("code-block", 23, Ui.Brush("#A8C4B9")), caption }
            };
            frame.Children.Add(new Border
            {
                Background = Ui.Brush("#1B3028"),
                BorderBrush = Ui.Brush("#365047"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(22, 0),
                Child = header
            });
            Grid.SetRow(body, 1);
        }

        frame.Children.Add(body);

        return new Border
        {
            Background = Ui.Brush("#13241F"),
            BorderBrush = Ui.Brush("#344C42"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Child = frame
        };
    }

    private static void AppendBlocks(StackPanel destination, ContainerBlock blocks, SlideElement style)
    {
        foreach (Block block in blocks)
        {
            switch (block)
            {
                case ListBlock list:
                    AppendList(destination, list, style);
                    break;
                case QuoteBlock quote:
                    StackPanel quoted = new() { Spacing = destination.Spacing };
                    AppendBlocks(quoted, quote, style);

                    destination.Children.Add(new Border
                    {
                        BorderBrush = Ui.Green,
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        Padding = new Thickness(style.FontSize * .5, 0, 0, 0),
                        Child = quoted
                    });
                    break;
                case ThematicBreakBlock:
                    destination.Children.Add(new Border { Height = 2, Background = Ui.Brush(style.Color), Opacity = .4 });
                    break;
                case CodeBlock code:
                    TextBlock raw = NewText(style);
                    raw.FontFamily = Ui.Mono;
                    raw.Text = code.Lines.ToString();
                    destination.Children.Add(raw);
                    break;
                case LeafBlock leaf:
                    TextBlock text = NewText(style);
                    if (leaf is HeadingBlock heading)
                    {
                        text.FontSize *= 1.6 - ((heading.Level - 1) * .09);
                        text.FontWeight = FontWeight.Bold;
                    }

                    if (text.Inlines != null)
                        AppendInlines(text.Inlines, leaf.Inline);

                    destination.Children.Add(text);
                    break;
            }
        }
    }

    private static void AppendList(StackPanel destination, ListBlock list, SlideElement style)
    {
        int index = int.TryParse(list.OrderedStart, out int start) ? start : 1;
        foreach (Block item in list)
        {
            if (item is not ListItemBlock listItem)
                continue;

            TextBlock marker = NewText(style);
            marker.Text = list.IsOrdered ? $"{index++}." : "•";
            marker.TextAlignment = TextAlignment.Left;
            marker.Margin = new Thickness(0, 0, style.FontSize * .35, 0);

            StackPanel body = new() { Spacing = destination.Spacing };
            AppendBlocks(body, listItem, style);

            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { marker, body } };
            Grid.SetColumn(body, 1);
            destination.Children.Add(row);
        }
    }

    private static void AppendInlines(InlineCollection target, ContainerInline? source)
    {
        for (Markdig.Syntax.Inlines.Inline? inline = source?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;
                case LineBreakInline line:
                    target.Add(line.IsHard ? new LineBreak() : new Run(" "));
                    break;
                case EmphasisInline emphasis:
                    Span styled = emphasis.DelimiterCount > 1 ? new Bold() : new Italic();
                    AppendInlines(styled.Inlines, emphasis);
                    target.Add(styled);
                    break;
                case CodeInline code:
                    target.Add(new Run(code.Content) { FontFamily = Ui.Mono, Foreground = Ui.Brush("#1D7052") });
                    break;
                case LinkInline link:
                    Underline label = new();
                    AppendInlines(label.Inlines, link);
                    target.Add(label);
                    break;
                case AutolinkInline link:
                    target.Add(new Run(link.Url));
                    break;
                case HtmlEntityInline entity:
                    target.Add(new Run(entity.Transcoded.ToString()));
                    break;
                case HtmlInline html:
                    target.Add(new Run(html.Tag));
                    break;
                case ContainerInline container:
                    AppendInlines(target, container);
                    break;
            }
        }
    }

    private static TextBlock NewText(SlideElement style)
    {
        return new TextBlock
        {
            FontFamily = FontCatalog.Resolve(style.Font),
            FontSize = style.FontSize,
            FontWeight = style.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = style.Italic ? FontStyle.Italic : FontStyle.Normal,
            Foreground = Ui.Brush(style.Color),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = style.Align switch
            {
                TextAlign.Center => TextAlignment.Center,
                TextAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            }
        };
    }

    [GeneratedRegex("""(?<comment>//[^\n]*|/\*[\s\S]*?\*/|^\s*\#[^\n]*)|(?<string>"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)|(?<keyword>\b(?:const|let|var|async|await|function|return|if|else|for|class|new|using|public|private|static|def|import|from|null|true|false|throw)\b)|(?<number>\b\d+(?:\.\d+)?\b)|(?<call>\b[A-Za-z_$][\w$]*(?=\s*\())""",
        RegexOptions.Multiline, 250)]
    private static partial Regex CodeTokens();
}
