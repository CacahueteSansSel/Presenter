using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Presenter.Views;

internal static class Ui
{
    private static readonly Dictionary<string, IBrush> BrushesByColor = new(StringComparer.Ordinal);
    private static readonly Lazy<Dictionary<string, IconPart[]>> Icons = new(ReadIcons);

    public static readonly FontFamily Inter = new("avares://Avalonia.Fonts.Inter/Assets#Inter");
    public static readonly FontFamily Mono = new("avares://Presenter/Assets/Fonts#JetBrains Mono");
    public static readonly IBrush Ink = Brush("#000000");
    public static readonly IBrush Muted = Brush("#656565");
    public static readonly IBrush Line = Brush("#E3E3E3");
    public static readonly IBrush Green = Brush("#1D8059");

    public static IBrush Brush(string color)
    {
        if (!BrushesByColor.TryGetValue(color, out IBrush? brush))
        {
            brush = new SolidColorBrush(Color.TryParse(color, out Color value) ? value : Colors.Black);
            if (BrushesByColor.Count < 256)
                BrushesByColor[color] = brush;
        }

        return brush;
    }

    public static TextBlock Text(string value, double size = 13, IBrush? color = null, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = value,
            FontSize = size,
            Foreground = color ?? Ink,
            FontWeight = weight ?? FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static TextBlock Meta(string value)
    {
        TextBlock text = Text(value);
        text.Classes.Add("meta");

        return text;
    }

    public static Control Icon(string name, double size = 17, IBrush? color = null) =>
        new IconGlyph { Glyph = name, Ink = color, Width = size, Height = size };

    private static Dictionary<string, IconPart[]> ReadIcons()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://Presenter/Assets/icons.json"));

        Dictionary<string, string> sources = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        return sources.ToDictionary(entry => entry.Key, entry => XDocument.Parse("<svg>" + entry.Value + "</svg>")
            .Descendants()
            .Where(element => element.Name.LocalName != "svg" && element.Name.LocalName != "g")
            .Select(ToIconPart)
            .ToArray());
    }

    private static IconPart ToIconPart(XElement shape)
    {
        Geometry geometry = shape.Name.LocalName switch
        {
            "path" => Geometry.Parse((string?)shape.Attribute("d") ?? ""),
            "circle" => new EllipseGeometry(new Rect(
                Number(shape, "cx") - Number(shape, "r"), Number(shape, "cy") - Number(shape, "r"),
                Number(shape, "r") * 2, Number(shape, "r") * 2)),
            _ => new RectangleGeometry(new Rect(
                Number(shape, "x"), Number(shape, "y"), Number(shape, "width"), Number(shape, "height")))
        };

        return new IconPart(geometry, Attribute(shape, "fill") != "none", Attribute(shape, "stroke") != null,
            double.TryParse(Attribute(shape, "stroke-width"), System.Globalization.CultureInfo.InvariantCulture,
                out double stroke) ? stroke : 1);
    }

    private static string? Attribute(XElement shape, string name) =>
        shape.AncestorsAndSelf().Select(element => (string?)element.Attribute(name)).FirstOrDefault(value => value != null);

    private static double Number(XElement shape, string name) =>
        double.TryParse((string?)shape.Attribute(name), System.Globalization.CultureInfo.InvariantCulture,
            out double number) ? number : 0;

    private sealed record IconPart(Geometry Geometry, bool Fill, bool Stroke, double Thickness);

    public static void DrawIcon(DrawingContext context, string glyph, IBrush brush, Size size)
    {
        if (!Icons.Value.TryGetValue(glyph, out IconPart[]? parts))
            return;

        using DrawingContext.PushedState state = context.PushTransform(Matrix.CreateScale(size.Width / 24, size.Height / 24));
        foreach (IconPart part in parts)
        {
            Pen? pen = part.Stroke ? new Pen(brush, part.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null;
            context.DrawGeometry(part.Fill ? brush : null, pen, part.Geometry);
        }
    }
}
