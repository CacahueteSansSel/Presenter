using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Presenter.Documents;

namespace Presenter.Views;

internal sealed partial class MainWindow
{
    private bool _syncingInspector;
    private string[] _layoutKeys = [];

    private void ConnectInspector()
    {
        DeckNameField.TextChanged += (_, _) =>
        {
            if (_syncingInspector)
                return;

            _deck.Name = DisplayName(DeckNameField.Text);
            Record("deck-name");
        };
        SlideNameField.TextChanged += (_, _) =>
        {
            if (_syncingInspector || CurrentSlide.Name == DisplayName(SlideNameField.Text))
                return;

            CurrentSlide.Name = DisplayName(SlideNameField.Text);
            Record($"slide-name:{_slideId}");
            BuildSlideList();
        };
        NotesField.TextChanged += (_, _) =>
        {
            if (_syncingInspector)
                return;

            CurrentSlide.Notes = NotesField.Text ?? "";
            Record($"notes:{_slideId}");
        };
        LayoutField.SelectionChanged += (_, _) =>
        {
            int index = LayoutField.SelectedIndex;
            if (_syncingInspector || index < 0 || index >= _layoutKeys.Length ||
                CurrentSlide.Layout == _layoutKeys[index])
            {
                return;
            }

            CurrentSlide.Layout = _layoutKeys[index];
            Record();
            Refresh();
        };

        CommitOnExit(ElementIdField, RenameElement);
        CommitOnExit(ColorField, CommitColor);
        CaptionField.TextChanged += (_, _) => EditElement(element => element.Caption = CaptionField.Text ?? "", "caption");
        ContentField.TextChanged += (_, _) => EditElement(element => element.Content = ContentField.Text ?? "", "content");
        FontField.SelectionChanged += (_, _) =>
        {
            if (FontField.SelectedItem is FontChoice font)
                EditElement(element => element.Font = font.Reference);
        };
        AlignmentField.SelectionChanged += (_, _) =>
        {
            if (AlignmentField.SelectedIndex >= 0)
                EditElement(element => element.Align = (TextAlign)AlignmentField.SelectedIndex);
        };
        HiddenField.IsCheckedChanged += (_, _) => EditElement(element => element.Hidden = HiddenField.IsChecked == true);

        ConnectNumber(XField, element => element.X, (element, value) => element.X = value, -1200, 1200);
        ConnectNumber(YField, element => element.Y, (element, value) => element.Y = value, -675, 675);
        ConnectNumber(WidthField, element => element.Width, (element, value) => element.Width = value, 1, 2400);
        ConnectNumber(HeightField, element => element.Height, (element, value) => element.Height = value, 1, 1350);
        ConnectNumber(AngleField, element => element.Angle, (element, value) => element.Angle = value, -360, 360);
        ConnectNumber(FontSizeField, element => element.FontSize, (element, value) => element.FontSize = value, 6, 250);
    }

    private void BuildInspector()
    {
        _syncingInspector = true;
        try
        {
            SlideElement? element = CurrentElement;
            SlideInspector.IsVisible = element == null;
            ElementInspector.IsVisible = element != null;

            if (element == null)
                RefreshSlideFields();
            else
                RefreshElementFields(element);
        }
        finally
        {
            _syncingInspector = false;
        }
    }

    private void RefreshSlideFields()
    {
        Slide slide = CurrentSlide;
        DeckNameField.Text = _deck.Name;
        SlideNameField.Text = slide.Name;
        NotesField.Text = slide.Notes;

        _layoutKeys = new[] { "title", "slide", "end" }.Concat(CurrentTheme.Layouts.Keys)
            .Append(slide.Layout)
            .Append("blank")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        LayoutField.ItemsSource = _layoutKeys.Select(CurrentTheme.GetLabel).ToArray();
        LayoutField.SelectedIndex = Array.IndexOf(_layoutKeys, slide.Layout);
    }

    private void RefreshElementFields(SlideElement element)
    {
        bool text = element.Kind == ElementKind.Text;
        bool image = element.Kind == ElementKind.Image;

        ElementIdField.Text = element.Id;
        ImageFields.IsVisible = image;
        TextFields.IsVisible = !image;
        CaptionPanel.IsVisible = element.Kind == ElementKind.Window;
        ColorPanel.IsVisible = text;
        MarkdownTools.IsVisible = text;
        AlignmentField.IsVisible = text;

        if (image)
        {
            ImageFilename.Text = element.Caption;
        }
        else
        {
            CaptionField.Text = element.Caption;
            ContentLabel.Text = text ? "Texte (Markdown)" : "Code source";
            ContentField.FontFamily = text ? Ui.Inter : Ui.Mono;
            ContentField.Text = element.Content;
            FontSizeField.Text = FormatNumber(element.FontSize);
            ColorField.Text = element.Color;
            AlignmentField.SelectedIndex = (int)element.Align;

            FontChoice[] choices = FontCatalog.Choices(_fonts.Keys);
            FontChoice? selected = choices.FirstOrDefault(choice => choice.Reference == element.Font);
            if (selected == null)
            {
                selected = new FontChoice(element.Font, element.Font);
                choices = [..choices, selected];
            }

            FontField.ItemsSource = choices;
            FontField.SelectedItem = selected;
            UpdateStyleButtons();
        }

        XField.Text = FormatNumber(element.X);
        YField.Text = FormatNumber(element.Y);
        WidthField.Text = FormatNumber(element.Width);
        HeightField.Text = FormatNumber(element.Height);
        AngleField.Text = FormatNumber(element.Angle);
        HiddenField.IsChecked = element.Hidden;
    }

    private void OnDuplicateSlide(object? sender, RoutedEventArgs args) => DuplicateSlide();
    private void OnDeleteSlide(object? sender, RoutedEventArgs args) => DeleteSlide();
    private void OnDuplicateElement(object? sender, RoutedEventArgs args) => DuplicateElement();
    private void OnDeleteElement(object? sender, RoutedEventArgs args) => DeleteElement();
    private void OnReplaceImage(object? sender, RoutedEventArgs args) => RunImageImport(replace: true);

    private async void OnBrowseFonts(object? sender, RoutedEventArgs args)
    {
        await RunFileAsync(async () =>
        {
            await Launcher.LaunchUriAsync(new Uri("https://fonts.google.com/"));
        });
    }

    private async void OnImportFont(object? sender, RoutedEventArgs args)
    {
        if (CurrentElement is SlideElement element)
            await RunFileAsync(() => ImportFontAsync(element));
    }

    private void OnFontBold(object? sender, RoutedEventArgs args)
    {
        EditElement(element => element.Bold = !element.Bold);
        UpdateStyleButtons();
    }

    private void OnFontItalic(object? sender, RoutedEventArgs args)
    {
        EditElement(element => element.Italic = !element.Italic);
        UpdateStyleButtons();
    }

    private void UpdateStyleButtons()
    {
        BoldButton.Classes.Set("active", CurrentElement?.Bold == true);
        ItalicButton.Classes.Set("active", CurrentElement?.Italic == true);
    }

    private void OnMarkdownBold(object? sender, RoutedEventArgs args) => WrapSelection("**", "**");
    private void OnMarkdownItalic(object? sender, RoutedEventArgs args) => WrapSelection("*", "*");
    private void OnMarkdownCode(object? sender, RoutedEventArgs args) => WrapSelection("`", "`");
    private void OnMarkdownBullet(object? sender, RoutedEventArgs args) => WrapSelection("- ", "");

    private void WrapSelection(string prefix, string suffix)
    {
        int start = Math.Min(ContentField.SelectionStart, ContentField.SelectionEnd);
        int end = Math.Max(ContentField.SelectionStart, ContentField.SelectionEnd);
        string original = ContentField.Text ?? "";
        ContentField.Text = original[..start] + prefix + original[start..end] + suffix + original[end..];
        ContentField.SelectionStart = start + prefix.Length;
        ContentField.SelectionEnd = end + prefix.Length;
        ContentField.Focus();
    }

    private void OnPreviewNotes(object? sender, RoutedEventArgs args)
    {
        NotesWindow preview = new(CurrentSlide.Name, NotesField.Text ?? "");
        _ = preview.ShowDialog(this);
    }

    private void RenameElement()
    {
        if (_syncingInspector || CurrentElement is not SlideElement element)
            return;

        string candidate = ElementIdField.Text?.Trim() ?? "";
        if (candidate == element.Id)
            return;

        if (!Templates.IsValidId(candidate) || CurrentSlide.Elements.Any(item => item.Id == candidate))
        {
            ElementIdField.Text = element.Id;
            UpdateStatus("Choisissez un identifiant unique avec des lettres, chiffres, tirets ou underscores.", error: true);

            return;
        }

        element.Id = candidate;
        _elementId = candidate;
        Record();
        Surface.Load(CurrentSlide, CurrentTheme, _images.Resolve, animate: false);
        Surface.Select(candidate, _gizmo);
        BuildElementList();
        RefreshThumbnail();
        UpdateStatus("Identifiant modifié. Vérifiez ses références dans le Code Flow.");
    }

    private void CommitColor()
    {
        if (_syncingInspector || CurrentElement is not SlideElement element)
            return;

        string input = ColorField.Text?.Trim() ?? "";
        if (Color.TryParse(input, out Color color) && input != element.Color)
            EditElement(selected => selected.Color = color.ToString());

        ColorField.Text = element.Color;
    }

    private void EditElement(Action<SlideElement> edit, string? field = null)
    {
        if (_syncingInspector || CurrentElement is not SlideElement element)
            return;

        edit(element);
        Record(field == null ? null : $"element:{_slideId}:{element.Id}:{field}");
        Surface.Update(element);
        RefreshThumbnail();
    }

    private void ConnectNumber(
        TextBox field, Func<SlideElement, double> get, Action<SlideElement, double> set, double min, double max)
    {
        CommitOnExit(field, () =>
        {
            if (_syncingInspector || CurrentElement is not SlideElement element)
                return;

            double current = get(element);
            if (field.Text != FormatNumber(current) &&
                double.TryParse(field.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) &&
                double.IsFinite(value))
            {
                EditElement(selected => set(selected, Math.Clamp(value, min, max)));
            }

            field.Text = FormatNumber(get(element));
        });
    }

    private static void CommitOnExit(TextBox field, Action commit)
    {
        field.LostFocus += (_, _) => commit();
        field.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                commit();
                args.Handled = true;
            }
        };
    }

    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static string DisplayName(string? input) =>
        string.IsNullOrWhiteSpace(input) ? "Sans titre" : input.Trim();
}
