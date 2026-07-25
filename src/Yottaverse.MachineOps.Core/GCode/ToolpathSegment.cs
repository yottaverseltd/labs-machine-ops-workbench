namespace Yottaverse.MachineOps.Core.GCode;

public enum MotionMode
{
    Rapid,
    Linear,
}

public sealed record ToolpathSegment(
    Position3D From,
    Position3D To,
    MotionMode Mode,
    int SourceLine,
    double? FeedRate);
