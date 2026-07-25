using System.ComponentModel.DataAnnotations;

namespace Yottaverse.MachineOps.Contracts.Alarms;

public sealed record AcknowledgeAlarmRequest(
    [Required] Guid IdempotencyKey,
    [Required, StringLength(120, MinimumLength = 2)] string AcknowledgedBy,
    [StringLength(500)] string? Note,
    [Range(0, int.MaxValue)] int ExpectedVersion);

public sealed record AlarmDto(
    Guid Id,
    Guid MachineId,
    Guid? JobRunId,
    string Code,
    string Severity,
    string Message,
    DateTimeOffset RaisedAtUtc,
    int Version,
    bool IsAcknowledged,
    string? AcknowledgedBy,
    DateTimeOffset? AcknowledgedAtUtc,
    string? Note);

public sealed record AlarmNotificationDto(
    Guid Id,
    Guid MachineId,
    string Code,
    string Message,
    int Version,
    bool IsAcknowledged,
    DateTimeOffset RaisedAtUtc);
