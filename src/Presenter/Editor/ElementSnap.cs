using Avalonia;
using Presenter.Documents;

namespace Presenter.Editor;

internal static class ElementSnap
{
    public static Rect Bounds(SlideElement element)
    {
        double radians = element.Angle * Math.PI / 180;
        double sin = Math.Abs(Math.Sin(radians));
        double cos = Math.Abs(Math.Cos(radians));

        double width = cos * element.Width + sin * element.Height;
        double height = sin * element.Width + cos * element.Height;
        double x = element.X + (element.Width - width) / 2;
        double y = element.Y + (element.Height - height) / 2;

        return new Rect(x, y, width, height);
    }

    public static Alignment Match(double start, double length, IReadOnlyList<double> guides, double tolerance)
    {
        double distance = tolerance;
        Alignment result = default;
        Span<double> anchors = [start, start + length / 2, start + length];

        foreach (double anchor in anchors)
        {
            foreach (double guide in guides)
            {
                double offset = guide - anchor;
                double absolute = Math.Abs(offset);
                if (absolute >= distance)
                    continue;

                result = new Alignment(offset, guide);
                distance = absolute;
            }
        }

        return result;
    }

    internal readonly record struct Alignment(double Offset, double? Guide);
}
