using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Api.Mappings;

internal static class JobMappings
{
    public static JobDto ToDto(this MachiningJob job) =>
        new(
            job.Id,
            job.Name,
            job.State.ToString(),
            job.CreatedAtUtc,
            job.Program.Source,
            job.Program.Segments.Count,
            job.Program.TravelDistance,
            job.Program.Bounds.Width,
            job.Program.Bounds.Height,
            job.Program.Segments.Select(ToDto).ToArray(),
            job.Program.Diagnostics.Select(ToDto).ToArray());

    public static JobSummaryDto ToSummaryDto(this MachiningJob job) =>
        new(
            job.Id,
            job.Name,
            job.State.ToString(),
            job.CreatedAtUtc,
            job.Program.Segments.Count,
            job.Program.TravelDistance);

    public static GCodeDiagnosticDto ToDto(this GCodeDiagnostic diagnostic) =>
        new(
            diagnostic.LineNumber,
            diagnostic.Severity.ToString(),
            diagnostic.Code,
            diagnostic.Message);

    private static ToolpathSegmentDto ToDto(ToolpathSegment segment) =>
        new(
            new PositionDto(segment.From.X, segment.From.Y, segment.From.Z),
            new PositionDto(segment.To.X, segment.To.Y, segment.To.Z),
            segment.Mode.ToString(),
            segment.SourceLine,
            segment.FeedRate);
}
