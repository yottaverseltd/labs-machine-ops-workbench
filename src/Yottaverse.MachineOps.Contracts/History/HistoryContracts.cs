namespace Yottaverse.MachineOps.Contracts.History;

public sealed record JobHistoryDto(
    Guid Id,
    string Name,
    string State,
    DateTimeOffset CreatedAtUtc);

public sealed record RunHistoryDto(
    Guid Id,
    Guid JobId,
    string JobName,
    string State,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureReason);

public sealed record AlarmHistoryDto(
    Guid Id,
    string Code,
    string Severity,
    string Message,
    DateTimeOffset RaisedAtUtc,
    bool IsAcknowledged);

public sealed record ProtocolMessageDto(
    long Id,
    Guid SessionId,
    long Sequence,
    string Direction,
    string MessageType,
    string Payload,
    DateTimeOffset ObservedAtUtc);

public sealed record PageDto<T>(
    IReadOnlyList<T> Items,
    int Skip,
    int Take,
    long Total);

public sealed record OperationsHistoryDto(
    PageDto<JobHistoryDto> Jobs,
    PageDto<RunHistoryDto> Runs,
    PageDto<AlarmHistoryDto> Alarms,
    PageDto<ProtocolMessageDto> ProtocolMessages);
