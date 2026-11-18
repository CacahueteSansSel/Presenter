using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Presenter.Views;

internal sealed class IconGlyph : Control
{
    public static readonly StyledProperty<string> GlyphProperty =
        AvaloniaProperty.Register<IconGlyph, string>(nameof(Glyph), "");
    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<IconGlyph, IBrush?>(nameof(Ink));

    public string Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    static IconGlyph()
    {
        AffectsRender<IconGlyph>(GlyphProperty, InkProperty);
    }

    public override void Render(DrawingContext context) =>
        Ui.DrawIcon(context, Glyph, Ink ?? Ui.Muted, Bounds.Size);
}
