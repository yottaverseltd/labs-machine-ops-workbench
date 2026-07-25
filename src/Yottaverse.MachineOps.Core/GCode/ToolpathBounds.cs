namespace Yottaverse.MachineOps.Core.GCode;

public readonly record struct ToolpathBounds(
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY)
{
    public double Width => MaximumX - MinimumX;

    public double Height => MaximumY - MinimumY;

    public static ToolpathBounds From(IReadOnlyList<ToolpathSegment> segments)
    {
        if (segments.Count == 0)
        {
            return new ToolpathBounds(0, 0, 0, 0);
        }

        double minimumX = double.MaxValue;
        double minimumY = double.MaxValue;
        double maximumX = double.MinValue;
        double maximumY = double.MinValue;

        foreach (ToolpathSegment segment in segments)
        {
            minimumX = Math.Min(minimumX, Math.Min(segment.From.X, segment.To.X));
            minimumY = Math.Min(minimumY, Math.Min(segment.From.Y, segment.To.Y));
            maximumX = Math.Max(maximumX, Math.Max(segment.From.X, segment.To.X));
            maximumY = Math.Max(maximumY, Math.Max(segment.From.Y, segment.To.Y));
        }

        return new ToolpathBounds(minimumX, minimumY, maximumX, maximumY);
    }
}
