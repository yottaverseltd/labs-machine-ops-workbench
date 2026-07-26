using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Yottaverse.MachineOps.Core.GCode;

namespace Yottaverse.MachineOps.Desktop.Controls;

public sealed class ToolpathView : Control
{
    public static readonly StyledProperty<IReadOnlyList<ToolpathSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<ToolpathView, IReadOnlyList<ToolpathSegment>?>(nameof(Segments));

    public static readonly StyledProperty<Position3D?> CurrentPositionProperty =
        AvaloniaProperty.Register<ToolpathView, Position3D?>(nameof(CurrentPosition));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ToolpathView, double>(nameof(Progress));

    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<ToolpathView, bool>(nameof(IsLive));

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#091018"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#172433")), 1);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.Parse("#42546A")), 1);
    private static readonly Pen PlannedRapidPen =
        new(new SolidColorBrush(Color.Parse("#44566B")), 1.2, DashStyle.Dash);
    private static readonly Pen PlannedLinearPen =
        new(new SolidColorBrush(Color.Parse("#235346")), 2);
    private static readonly Pen CompletedRapidPen =
        new(new SolidColorBrush(Color.Parse("#8DA4BD")), 1.5, DashStyle.Dash);
    private static readonly Pen CompletedLinearPen =
        new(new SolidColorBrush(Color.Parse("#41D6A3")), 2.5);
    private static readonly IBrush ToolheadBrush = new SolidColorBrush(Color.Parse("#F7B84B"));
    private static readonly Pen ToolheadOutlinePen =
        new(new SolidColorBrush(Color.Parse("#FFF0C2")), 2);

    static ToolpathView()
    {
        AffectsRender<ToolpathView>(
            SegmentsProperty,
            CurrentPositionProperty,
            ProgressProperty,
            IsLiveProperty);
    }

    public IReadOnlyList<ToolpathSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public Position3D? CurrentPosition
    {
        get => GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(BackgroundBrush, Bounds);
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
            context.DrawLine(
                segment.Mode == MotionMode.Rapid ? PlannedRapidPen : PlannedLinearPen,
                from,
                to);
        }

        DrawCompletedPath(context, segments, toolpathBounds, scale, padding);
        if (IsLive && CurrentPosition is Position3D currentPosition)
        {
            DrawToolhead(context, Project(currentPosition, toolpathBounds, scale, padding));
        }
    }

    private void DrawCompletedPath(
        DrawingContext context,
        IReadOnlyList<ToolpathSegment> segments,
        ToolpathBounds bounds,
        double scale,
        double padding)
    {
        double progress = Math.Clamp(Progress, 0, 100);
        if (progress <= 0)
        {
            return;
        }

        double remainingDistance =
            segments.Sum(segment => segment.From.DistanceTo(segment.To)) * (progress / 100);
        foreach (ToolpathSegment segment in segments)
        {
            double segmentLength = segment.From.DistanceTo(segment.To);
            if (segmentLength <= 0)
            {
                continue;
            }

            Position3D completedTo = segment.To;
            bool partiallyComplete = remainingDistance < segmentLength;
            if (partiallyComplete)
            {
                double fraction = remainingDistance / segmentLength;
                completedTo = Interpolate(segment.From, segment.To, fraction);
            }

            context.DrawLine(
                segment.Mode == MotionMode.Rapid ? CompletedRapidPen : CompletedLinearPen,
                Project(segment.From, bounds, scale, padding),
                Project(completedTo, bounds, scale, padding));
            if (partiallyComplete)
            {
                break;
            }

            remainingDistance -= segmentLength;
            if (remainingDistance <= 0)
            {
                break;
            }
        }
    }

    private static Position3D Interpolate(Position3D from, Position3D to, double fraction) =>
        new(
            from.X + ((to.X - from.X) * fraction),
            from.Y + ((to.Y - from.Y) * fraction),
            from.Z + ((to.Z - from.Z) * fraction));

    private static void DrawToolhead(DrawingContext context, Point position)
    {
        Rect markerBounds = new(position.X - 6, position.Y - 6, 12, 12);
        context.DrawEllipse(ToolheadBrush, ToolheadOutlinePen, markerBounds);
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
