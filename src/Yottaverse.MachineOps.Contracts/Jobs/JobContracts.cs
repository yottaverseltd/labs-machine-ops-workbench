using System.ComponentModel.DataAnnotations;

namespace Yottaverse.MachineOps.Contracts.Jobs;

public sealed record CreateJobRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [Required] string GCode);

public sealed record PositionDto(double X, double Y, double Z);

public sealed record ToolpathSegmentDto(
    PositionDto From,
    PositionDto To,
    string Mode,
    int SourceLine,
    double? FeedRate);

public sealed record GCodeDiagnosticDto(
    int LineNumber,
    string Severity,
    string Code,
    string Message);

public sealed record JobDto(
    Guid Id,
    string Name,
    string State,
    DateTimeOffset CreatedAtUtc,
    string GCode,
    int SegmentCount,
    double TravelDistance,
    double WorkAreaWidth,
    double WorkAreaHeight,
    IReadOnlyList<ToolpathSegmentDto> Toolpath,
    IReadOnlyList<GCodeDiagnosticDto> Diagnostics);

public sealed record JobSummaryDto(
    Guid Id,
    string Name,
    string State,
    DateTimeOffset CreatedAtUtc,
    int SegmentCount,
    double TravelDistance);

public sealed record ApiStatusDto(
    string Service,
    string Version,
    DateTimeOffset ServerTimeUtc);
