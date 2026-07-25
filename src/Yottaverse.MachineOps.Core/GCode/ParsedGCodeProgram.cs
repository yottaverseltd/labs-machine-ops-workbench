namespace Yottaverse.MachineOps.Core.GCode;

public sealed record ParsedGCodeProgram(
    string Name,
    string Source,
    IReadOnlyList<ToolpathSegment> Segments,
    IReadOnlyList<GCodeDiagnostic> Diagnostics,
    ToolpathBounds Bounds)
{
    public bool IsValid => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);

    public double TravelDistance => Segments.Sum(segment => segment.From.DistanceTo(segment.To));
}
