using System.Text.RegularExpressions;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Indentation;
using Presenter.Flow;
using Presenter.Views;

namespace Presenter.Editor;

internal sealed partial class FlowEditor : TextEditor
{
    private CompletionWindow? _completion;

    public Func<IEnumerable<string>>? ElementIds { get; set; }

    public FlowEditor()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://Presenter/Assets/JavaScript.xshd"));
        using XmlReader reader = XmlReader.Create(stream);

        SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        FontFamily = Ui.Mono;
        FontSize = 13;
        ShowLineNumbers = true;
        LineNumbersForeground = Ui.Brush("#729281");
        Foreground = Ui.Brush("#243B31");
        Background = Ui.Brush("#F7FAF8");
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;

        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;
        TextArea.IndentationStrategy = new DefaultIndentationStrategy();
        TextArea.TextEntered += OnTextEntered;
        TextArea.TextEntering += OnTextEntering;
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public void DismissCompletion() => _completion?.Close();

    protected override void OnKeyDown(KeyEventArgs args)
    {
        if (args.Key == Key.Space && (args.KeyModifiers == KeyModifiers.Control || args.KeyModifiers == KeyModifiers.Meta))
        {
            Suggest();
            args.Handled = true;

            return;
        }

        base.OnKeyDown(args);
    }

    private void OnTextEntered(object? sender, TextInputEventArgs args)
    {
        if (_completion != null || string.IsNullOrEmpty(args.Text))
            return;

        char character = args.Text[^1];
        if (character == '"' || character == '\'' || char.IsLetter(character))
            Suggest();
    }

    private void OnTextEntering(object? sender, TextInputEventArgs args)
    {
        if (_completion == null || string.IsNullOrEmpty(args.Text))
            return;

        char character = args.Text[0];
        if (character == '(' || character == '"' || character == '\'')
            _completion.CompletionList.RequestInsertion(args);
    }

    private void Suggest()
    {
        if (_completion != null || Document == null)
            return;

        int offset = CaretOffset;
        DocumentLine line = Document.GetLineByOffset(offset);
        string before = Document.GetText(line.Offset, offset - line.Offset);

        Match identifier = ElementContext().Match(before);
        bool elementContext = identifier.Success;
        string prefix = elementContext ? identifier.Groups["id"].Value : WordAtEnd().Match(before).Value;
        if (!elementContext && (before.TrimStart().StartsWith("//", StringComparison.Ordinal) ||
            before.Count(character => character == '"') % 2 != 0 ||
            before.Count(character => character == '\'') % 2 != 0))
        {
            return;
        }

        IEnumerable<Suggestion> suggestions = elementContext
            ? (ElementIds?.Invoke() ?? []).Select(id => new Suggestion(id, "Élément", $"Identifiant de l’élément {id}"))
            : FlowApi.Methods.Select(method => new Suggestion(method.Name, method.Signature, method.Description))
                .Concat(new[] { "async", "await", "const", "let", "function", "return", "if", "else" }
                    .Select(keyword => new Suggestion(keyword, "JavaScript", "Mot-clé JavaScript")));

        Suggestion[] matches = suggestions.Where(item => item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Text != prefix)
            .ToArray();
        if (matches.Length == 0)
            return;

        CompletionWindow completion = new(TextArea)
        {
            StartOffset = offset - prefix.Length,
            EndOffset = offset,
            MinWidth = 230,
            MaxHeight = 270
        };

        foreach (Suggestion match in matches)
            completion.CompletionList.CompletionData.Add(match);

        completion.Closed += (_, _) => _completion = null;
        _completion = completion;
        completion.Show();
    }

    [GeneratedRegex("""\b(?:show|hide|fadeIn|fadeOut|text|move|rotate)\s*\(\s*["'](?<id>[\w-]*)$""")]
    private static partial Regex ElementContext();

    [GeneratedRegex(@"[\w$]+$")]
    private static partial Regex WordAtEnd();

    private sealed class Suggestion(string text, string signature, string description) : ICompletionData
    {
        public Avalonia.Media.IImage? Image => null;
        public string Text => text;
        public object Content => text;
        public object Description => $"{signature}\n{description}";
        public double Priority => 0;

        public void Complete(TextArea area, ISegment segment, EventArgs args) =>
            area.Document.Replace(segment, Text);
    }
}
