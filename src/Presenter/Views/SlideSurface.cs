using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Presenter.Documents;
using Presenter.Editor;

namespace Presenter.Views;

internal sealed class SlideSurface : Viewbox
{
    public const double StageWidth = 1200;
    public const double StageHeight = 675;

    private readonly Canvas _stage = new()
    {
        Width = StageWidth,
        Height = StageHeight,
        Background = Brushes.White,
        ClipToBounds = true
    };

    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    public bool Interactive { get; set; }
    private readonly Canvas _selection = new() { ZIndex = 10 };
    private readonly SnapGuides _guides = new();

    private Node? _selected;
    private Drag? _drag;
    private bool _gizmo;

    public event Action<SlideElement?>? SelectionChanged;
    public event Action? TransformCompleted;
    public event Action<SlideElement>? EditRequested;

    public SlideSurface() : this(interactive: true)
    {
    }

    public SlideSurface(bool interactive)
    {
        Interactive = interactive;
        Child = _stage;
        Stretch = Stretch.Uniform;

        _stage.PointerPressed += StagePressed;
        _stage.PointerMoved += StageMoved;
        _stage.PointerReleased += StageReleased;
        _stage.PointerCaptureLost += (_, _) => EndDrag();
    }

    public void Load(Slide slide, SlideTheme theme, Func<string?, Bitmap?> image, bool animate)
    {
        _nodes.Clear();
        _stage.Children.Clear();
        _selected = null;
        _drag = null;

        Bitmap? background = theme.GetBackground(slide.Layout);
        if (background != null)
        {
            _stage.Children.Add(new Image
            {
                Source = background,
                Width = StageWidth,
                Height = StageHeight,
                Stretch = Stretch.UniformToFill,
                IsHitTestVisible = false
            });
        }

        if (theme.Layouts.TryGetValue(slide.Layout, out ThemeLayout? layout))
        {
            foreach (ThemeMask mask in layout.Masks)
            {
                Border patch = new()
                {
                    Width = mask.Width,
                    Height = mask.Height,
                    Background = Ui.Brush(mask.Color),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(patch, mask.X);
                Canvas.SetTop(patch, mask.Y);
                _stage.Children.Add(patch);
            }
        }

        if (animate && theme.Animate && slide.Layout != "blank")
            _stage.Children.Add(new AmbientShapes());

        foreach (SlideElement element in slide.Elements)
        {
            Border frame = new() { ClipToBounds = true, Background = Brushes.Transparent };
            Node node = new(element, frame, image(element.Asset));
            _nodes[element.Id] = node;

            frame.Opacity = element.Hidden ? (Interactive ? .35 : 0) : 1;
            frame.Child = CreateVisual(node, element.Content);
            InstallGeometry(node);
            _stage.Children.Add(frame);

            if (Interactive)
                frame.PointerPressed += (_, args) => ElementPressed(node, args);
        }

        if (Interactive)
        {
            _stage.Children.Add(_selection);
            _guides.Show(null, null);
            _stage.Children.Add(_guides);
        }
    }

    public void Select(string? id, bool gizmo)
    {
        _gizmo = gizmo;
        _selected = id == null ? null : _nodes.GetValueOrDefault(id);
        _selection.Children.Clear();

        if (_selected == null)
            return;

        SlideElement element = _selected.Element;
        double width = element.Width;
        double height = element.Height;

        Border outline = new()
        {
            Width = width,
            Height = height,
            BorderBrush = Ui.Green,
            BorderThickness = new Thickness(2.4),
            IsHitTestVisible = false
        };
        _selection.Children.Add(outline);

        if (gizmo)
        {
            double x = width / 2;
            double y = height / 2;
            AddLine(x, y, x + 75, y, "#D45153");
            AddLine(x, y, x, y - 75, "#17965D");
            AddLine(x, y, x + 58, y + 58, "#326EB0");
            AddHandle(DragKind.MoveX, x + 75, y, "#D45153");
            AddHandle(DragKind.MoveY, x, y - 75, "#17965D");
            AddHandle(DragKind.Scale, x + 58, y + 58, "#326EB0");
            AddHandle(DragKind.Move, x, y, "#16724D");
        }
        else
        {
            AddHandle(DragKind.TopLeft, 0, 0);
            AddHandle(DragKind.TopRight, width, 0);
            AddHandle(DragKind.BottomLeft, 0, height);
            AddHandle(DragKind.BottomRight, width, height);
        }

        AddLine(width / 2, 0, width / 2, -32, "#1D8059");
        AddHandle(DragKind.Rotate, width / 2, -32);

        TextBlock label = Ui.Meta(element.Id);
        label.FontSize = 17;
        Border tag = new()
        {
            Background = Ui.Brush("#DFF1E6"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3),
            Child = label,
            IsHitTestVisible = false
        };
        Canvas.SetTop(tag, -29);
        _selection.Children.Add(tag);

        _selection.Width = width;
        _selection.Height = height;
        _selection.RenderTransform = new RotateTransform(element.Angle);
        InstallPosition(_selection, element.X, element.Y);
    }

    public void Update(SlideElement element)
    {
        if (!_nodes.TryGetValue(element.Id, out Node? node))
            return;

        node.Frame.Child = CreateVisual(node, element.Content);
        node.Frame.Opacity = element.Hidden ? .35 : 1;
        InstallGeometry(node);
        Select(_selected?.Element.Id, _gizmo);
    }

    public void Apply(string operation, string id, string text, double x, double y)
    {
        Node node = GetNode(id);

        switch (operation)
        {
            case "show":
                node.FadeVersion++;
                node.Frame.Opacity = 1;
                break;
            case "hide":
                node.FadeVersion++;
                node.Frame.Opacity = 0;
                break;
            case "text":
                node.Frame.Child = CreateVisual(node, text);
                break;
            case "move":
                InstallPosition(node.Frame, x, y);
                break;
            case "rotate":
                node.Frame.RenderTransform = new RotateTransform(x);
                break;
        }
    }

    public async Task FadeAsync(string id, double opacity, int duration, CancellationToken cancellationToken)
    {
        Node node = GetNode(id);
        int version = ++node.FadeVersion;

        double start = node.Frame.Opacity;
        if (duration > 0 && start == opacity && opacity == 1)
            start = node.Frame.Opacity = 0;

        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < duration && node.FadeVersion == version)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double fraction = Math.Min(1, elapsed.Elapsed.TotalMilliseconds / duration);
            double eased = fraction * fraction * (3 - 2 * fraction);
            node.Frame.Opacity = start + (opacity - start) * eased;

            await Task.Delay(16, cancellationToken);
        }

        if (node.FadeVersion == version)
            node.Frame.Opacity = opacity;
    }

    private Node GetNode(string id)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            throw new ArgumentException($"Élément « {id} » introuvable.");

        return node;
    }

    private static Control CreateVisual(Node node, string content)
    {
        SlideElement element = node.Element;
        return element.Kind switch
        {
            ElementKind.Text => SlideText.MarkdownText(element, content),
            ElementKind.Code or ElementKind.Window => SlideText.RenderCode(element, content),
            ElementKind.Image => node.Image == null
                ? Ui.Text("Image indisponible", 24, Ui.Muted)
                : new Image { Source = node.Image, Stretch = Stretch.Uniform },
            _ => new Border()
        };
    }

    private static void InstallPosition(Control control, double x, double y)
    {
        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);
    }

    private static void InstallGeometry(Node node)
    {
        SlideElement element = node.Element;
        node.Frame.Width = element.Width;
        node.Frame.Height = element.Height;
        node.Frame.RenderTransform = new RotateTransform(element.Angle);
        InstallPosition(node.Frame, element.X, element.Y);
    }

    private void AddLine(double x1, double y1, double x2, double y2, string color)
    {
        _selection.Children.Add(new Line
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(x2, y2),
            Stroke = Ui.Brush(color),
            StrokeThickness = 3,
            IsHitTestVisible = false
        });
    }

    private void AddHandle(DragKind kind, double x, double y, string color = "#1D8059")
    {
        bool round = kind == DragKind.Rotate || kind == DragKind.Move;
        Border handle = new()
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(round ? 9 : 2),
            BorderThickness = new Thickness(2.8),
            BorderBrush = Ui.Brush(color),
            Background = Brushes.White,
            Cursor = new Cursor(kind switch
            {
                DragKind.TopLeft or DragKind.BottomRight => StandardCursorType.TopLeftCorner,
                DragKind.TopRight or DragKind.BottomLeft => StandardCursorType.TopRightCorner,
                DragKind.MoveX => StandardCursorType.SizeWestEast,
                DragKind.MoveY => StandardCursorType.SizeNorthSouth,
                DragKind.Move => StandardCursorType.SizeAll,
                _ => StandardCursorType.Cross
            })
        };
        InstallPosition(handle, x - 9, y - 9);
        handle.PointerPressed += (_, args) =>
        {
            if (_selected != null)
                BeginDrag(_selected, kind, args);
        };

        _selection.Children.Add(handle);
    }

    private void ElementPressed(Node node, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_stage).Properties.IsLeftButtonPressed)
            return;

        if (_selected != node)
        {
            Select(node.Element.Id, _gizmo);
            SelectionChanged?.Invoke(node.Element);
        }

        if (args.ClickCount == 2)
        {
            EditRequested?.Invoke(node.Element);
            args.Handled = true;

            return;
        }

        BeginDrag(node, DragKind.Move, args);
    }

    private void StagePressed(object? sender, PointerPressedEventArgs args)
    {
        if (_selected == null || !args.GetCurrentPoint(_stage).Properties.IsLeftButtonPressed)
            return;

        Select(null, _gizmo);
        SelectionChanged?.Invoke(null);
    }

    private void BeginDrag(Node node, DragKind kind, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_stage).Properties.IsLeftButtonPressed)
            return;

        SlideElement element = node.Element;
        Rect[] targets = _nodes.Values.Where(target => target != node)
            .Select(target => ElementSnap.Bounds(target.Element))
            .ToArray();

        _drag = new Drag(
            node, kind, args.GetPosition(_stage), new Point(element.X, element.Y),
            new Size(element.Width, element.Height), element.Angle,
            targets.SelectMany(target => new[] { target.X, target.Center.X, target.Right }).Distinct().ToArray(),
            targets.SelectMany(target => new[] { target.Y, target.Center.Y, target.Bottom }).Distinct().ToArray());
        args.Pointer.Capture(_stage);
        args.Handled = true;
    }

    private void StageMoved(object? sender, PointerEventArgs args)
    {
        if (_drag is not Drag drag)
            return;

        Point position = args.GetPosition(_stage);
        Vector delta = position - drag.Pointer;
        if (!drag.Changed && delta.Length < 3)
            return;

        drag.Changed = true;
        SlideElement element = drag.Node.Element;

        switch (drag.Kind)
        {
            case DragKind.Move:
            case DragKind.MoveX:
            case DragKind.MoveY:
                element.X = Math.Clamp(drag.Position.X + (drag.Kind == DragKind.MoveY ? 0 : delta.X), 24 - element.Width, StageWidth - 24);
                element.Y = Math.Clamp(drag.Position.Y + (drag.Kind == DragKind.MoveX ? 0 : delta.Y), 24 - element.Height, StageHeight - 24);
                SnapMove(drag, args.KeyModifiers.HasFlag(KeyModifiers.Alt));
                break;
            case DragKind.Rotate:
                Vector from = drag.Pointer - drag.Center;
                Vector to = position - drag.Center;
                double rotation = (Math.Atan2(to.Y, to.X) - Math.Atan2(from.Y, from.X)) * 180 / Math.PI;
                element.Angle = (drag.Angle + rotation) % 360;
                break;
            case DragKind.Scale:
                Vector initial = drag.Pointer - drag.Center;
                Vector current = position - drag.Center;
                double ratio = Math.Clamp(current.Length / initial.Length, .15, 4);
                element.Width = Math.Clamp(drag.Size.Width * ratio, 35, StageWidth * 2);
                element.Height = Math.Clamp(drag.Size.Height * ratio, 35, StageHeight * 2);
                element.X = drag.Center.X - element.Width / 2;
                element.Y = drag.Center.Y - element.Height / 2;
                break;
            default:
                Resize(drag, delta);
                break;
        }

        InstallGeometry(drag.Node);
        Select(element.Id, _gizmo);
    }

    private void SnapMove(Drag drag, bool bypass)
    {
        if (bypass)
        {
            _guides.Show(null, null);

            return;
        }

        double scale = Math.Min(Bounds.Width / StageWidth, Bounds.Height / StageHeight);
        double tolerance = 7 / Math.Max(.05, scale);
        SlideElement element = drag.Node.Element;
        Rect bounds = ElementSnap.Bounds(element);

        ElementSnap.Alignment horizontal = drag.Kind == DragKind.MoveY
            ? default
            : ElementSnap.Match(bounds.X, bounds.Width, drag.HorizontalGuides, tolerance);
        ElementSnap.Alignment vertical = drag.Kind == DragKind.MoveX
            ? default
            : ElementSnap.Match(bounds.Y, bounds.Height, drag.VerticalGuides, tolerance);

        element.X += horizontal.Offset;
        element.Y += vertical.Offset;
        _guides.Show(horizontal.Guide, vertical.Guide);
    }

    private static void Resize(Drag drag, Vector delta)
    {
        SlideElement element = drag.Node.Element;
        int xSign = drag.Kind == DragKind.TopLeft || drag.Kind == DragKind.BottomLeft ? -1 : 1;
        int ySign = drag.Kind == DragKind.TopLeft || drag.Kind == DragKind.TopRight ? -1 : 1;

        double radians = drag.Angle * Math.PI / 180;
        Vector local = Rotate(delta, -radians);
        element.Width = Math.Clamp(drag.Size.Width + local.X * xSign, 35, StageWidth * 2);
        element.Height = Math.Clamp(drag.Size.Height + local.Y * ySign, 35, StageHeight * 2);

        Vector shift = new(
            (element.Width - drag.Size.Width) * xSign / 2,
            (element.Height - drag.Size.Height) * ySign / 2);
        Point center = drag.Center + Rotate(shift, radians);
        element.X = center.X - element.Width / 2;
        element.Y = center.Y - element.Height / 2;
    }

    private static Vector Rotate(Vector value, double radians)
    {
        double c = Math.Cos(radians);
        double s = Math.Sin(radians);

        return new Vector(value.X * c - value.Y * s, value.X * s + value.Y * c);
    }

    private void StageReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_drag == null)
            return;

        EndDrag();
        args.Pointer.Capture(null);
    }

    private void EndDrag()
    {
        bool changed = _drag?.Changed == true;
        _drag = null;
        _guides.Show(null, null);

        if (changed)
            TransformCompleted?.Invoke();
    }

    private sealed record Node(SlideElement Element, Border Frame, Bitmap? Image)
    {
        public int FadeVersion { get; set; }
    }

    private sealed record Drag(
        Node Node, DragKind Kind, Point Pointer, Point Position, Size Size, double Angle,
        double[] HorizontalGuides, double[] VerticalGuides)
    {
        public Point Center => Position + new Vector(Size.Width / 2, Size.Height / 2);
        public bool Changed { get; set; }
    }

    private enum DragKind
    {
        Move,
        MoveX,
        MoveY,
        Scale,
        Rotate,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private sealed class SnapGuides : Control
    {
        private readonly Pen _pen = new(Ui.Brush("#17A675"), 2, new DashStyle([5, 4], 0));
        private double? _x;
        private double? _y;

        public SnapGuides()
        {
            Width = StageWidth;
            Height = StageHeight;
            ZIndex = 11;
            IsHitTestVisible = false;
        }

        public void Show(double? x, double? y)
        {
            if (_x == x && _y == y)
                return;

            _x = x;
            _y = y;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            if (_x is double x)
                context.DrawLine(_pen, new Point(x, 0), new Point(x, StageHeight));

            if (_y is double y)
                context.DrawLine(_pen, new Point(0, y), new Point(StageWidth, y));
        }
    }

    private sealed class AmbientShapes : Control
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
        private readonly Stopwatch _elapsed = new();

        public AmbientShapes()
        {
            Width = StageWidth;
            Height = StageHeight;
            IsHitTestVisible = false;
            _timer.Tick += (_, _) => InvalidateVisual();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
        {
            base.OnAttachedToVisualTree(args);
            _elapsed.Start();
            _timer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
        {
            _timer.Stop();
            _elapsed.Stop();
            base.OnDetachedFromVisualTree(args);
        }

        public override void Render(DrawingContext context)
        {
            double t = _elapsed.Elapsed.TotalSeconds / 10;
            Point center = new(1020 + 31 * Math.Sin(t), 165 + 42 * Math.Cos(t));
            context.DrawEllipse(Ui.Brush("#113B9D6D"), null, center, 150, 150);
            context.DrawEllipse(null, new Pen(Ui.Brush("#2676A990"), 1.6),
                new Point(1070 + 26 * Math.Cos(t), 510 + 38 * Math.Sin(t)), 170, 170);
        }
    }
}
