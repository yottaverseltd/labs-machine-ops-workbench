using System.ComponentModel.DataAnnotations;

namespace Yottaverse.MachineOps.Contracts.Runs;

public sealed record StartRunRequest(
    [Required] Guid JobId);

public sealed record JobRunDto(
    Guid Id,
    Guid JobId,
    Guid MachineId,
    string State,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int LastCommandIndex,
    string? FailureReason);
