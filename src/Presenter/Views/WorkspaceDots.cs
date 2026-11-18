using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Presenter.Views;

internal sealed class WorkspaceDots : Control
{
    public override void Render(DrawingContext context)
    {
        IBrush ink = Ui.Brush("#CBD9D0");
        for (double y = 13; y < Bounds.Height; y += 19)
        {
            for (double x = 13; x < Bounds.Width; x += 19)
                context.DrawEllipse(ink, null, new Point(x, y), .85, .85);
        }
    }
}
