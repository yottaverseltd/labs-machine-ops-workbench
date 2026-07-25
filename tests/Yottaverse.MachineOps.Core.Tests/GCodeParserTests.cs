using Yottaverse.MachineOps.Core.GCode;

namespace Yottaverse.MachineOps.Core.Tests;

public sealed class GCodeParserTests
{
    private readonly GCodeParser parser = new();

    [Fact]
    public void ParseAbsoluteLinearMovesBuildsExpectedToolpath()
    {
        const string source =
            """
            G21 G90
            G0 X10 Y10
            G1 X20 Y10 F500
            G1 X20 Y25
            """;

        ParsedGCodeProgram result = parser.Parse("part.ngc", source);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Segments.Count);
        Assert.Equal(new Position3D(20, 25, 0), result.Segments[^1].To);
        Assert.Equal(0, result.Bounds.MinimumX);
        Assert.Equal(25, result.Bounds.MaximumY);
    }

    [Fact]
    public void ParseRelativeMovesAccumulatesPosition()
    {
        const string source =
            """
            G21 G91
            G1 X10 Y5 F100
            X-3 Y2
            """;

        ParsedGCodeProgram result = parser.Parse("relative.ngc", source);

        Assert.Equal(new Position3D(7, 7, 0), result.Segments[^1].To);
    }

    [Fact]
    public void ParseInchProgramConvertsCoordinatesToMillimetres()
    {
        const string source = "G20 G90 G1 X1 Y0.5 F10";

        ParsedGCodeProgram result = parser.Parse("imperial.nc", source);

        Assert.Equal(25.4, result.Segments[0].To.X, 5);
        Assert.Equal(12.7, result.Segments[0].To.Y, 5);
        Assert.Equal(254, result.Segments[0].FeedRate);
    }

    [Fact]
    public void ParseArcMoveReportsWarningAndSkipsSegment()
    {
        const string source =
            """
            G0 X0 Y0
            G2 X20 Y10 I10 J0
            """;

        ParsedGCodeProgram result = parser.Parse("arc.ngc", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GCODE002");
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void ParseNonPositiveFeedReportsValidationError()
    {
        ParsedGCodeProgram result = parser.Parse("invalid.ngc", "G1 X10 F0");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GCODE003");
    }

    [Fact]
    public void ParseEmptySourceReportsValidationError()
    {
        ParsedGCodeProgram result = parser.Parse("empty.ngc", string.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GCODE001");
    }
}
