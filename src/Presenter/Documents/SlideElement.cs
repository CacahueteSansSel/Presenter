namespace Presenter.Documents;

internal sealed class SlideElement
{
    public string Id { get; set; } = "";
    public ElementKind Kind { get; set; }
    public string Content { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Font { get; set; } = "Inter";
    public string Color { get; set; } = "#14271E";
    public string? Asset { get; set; }
    public TextAlign Align { get; set; }
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 380;
    public double Height { get; set; } = 100;
    public double Angle { get; set; }
    public double FontSize { get; set; } = 30;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Hidden { get; set; }
}

internal enum ElementKind
{
    Text,
    Code,
    Window,
    Image
}

internal enum TextAlign
{
    Left,
    Center,
    Right
}
