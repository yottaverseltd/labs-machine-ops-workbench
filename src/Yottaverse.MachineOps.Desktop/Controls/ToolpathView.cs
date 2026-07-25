using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Yottaverse.MachineOps.Core.GCode;

namespace Yottaverse.MachineOps.Desktop.Controls;

public sealed class ToolpathView : Control
{
    public static readonly StyledProperty<IReadOnlyList<ToolpathSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<ToolpathView, IReadOnlyList<ToolpathSegment>?>(nameof(Segments));

    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#172433")), 1);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.Parse("#42546A")), 1);
    private static readonly Pen RapidPen = new(new SolidColorBrush(Color.Parse("#60758D")), 1.2, DashStyle.Dash);
    private static readonly Pen LinearPen = new(new SolidColorBrush(Color.Parse("#41D6A3")), 2);

    static ToolpathView()
    {
        AffectsRender<ToolpathView>(SegmentsProperty);
    }

    public IReadOnlyList<ToolpathSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#091018")), Bounds);
        DrawGrid(context);

        if (Segments is not { Count: > 0 } segments)
        {
            return;
        }

        ToolpathBounds toolpathBounds = ToolpathBounds.From(segments);
        double width = Math.Max(toolpathBounds.Width, 1);
        double height = Math.Max(toolpathBounds.Height, 1);
        const double padding = 30;
        double scale = Math.Min(
            Math.Max(Bounds.Width - (padding * 2), 1) / width,
            Math.Max(Bounds.Height - (padding * 2), 1) / height);

        foreach (ToolpathSegment segment in segments)
        {
            Point from = Project(segment.From, toolpathBounds, scale, padding);
            Point to = Project(segment.To, toolpathBounds, scale, padding);
            context.DrawLine(segment.Mode == MotionMode.Rapid ? RapidPen : LinearPen, from, to);
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        const double spacing = 32;
        for (double x = 0; x < Bounds.Width; x += spacing)
        {
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, Bounds.Height));
        }

        for (double y = 0; y < Bounds.Height; y += spacing)
        {
            context.DrawLine(GridPen, new Point(0, y), new Point(Bounds.Width, y));
        }

        context.DrawLine(AxisPen, new Point(0, Bounds.Height - 1), new Point(Bounds.Width, Bounds.Height - 1));
        context.DrawLine(AxisPen, new Point(1, 0), new Point(1, Bounds.Height));
    }

    private Point Project(
        Position3D position,
        ToolpathBounds bounds,
        double scale,
        double padding)
    {
        double x = padding + ((position.X - bounds.MinimumX) * scale);
        double y = Bounds.Height - padding - ((position.Y - bounds.MinimumY) * scale);
        return new Point(x, y);
    }
}
