namespace Yottaverse.MachineOps.Application.Abstractions;

public sealed record JobHistoryItem(
    Guid Id,
    string Name,
    string State,
    DateTimeOffset CreatedAtUtc);

public sealed record RunHistoryItem(
    Guid Id,
    Guid JobId,
    string JobName,
    string State,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureReason);

public sealed record AlarmHistoryItem(
    Guid Id,
    string Code,
    string Severity,
    string Message,
    DateTimeOffset RaisedAtUtc,
    bool IsAcknowledged);

public sealed record ProtocolHistoryItem(
    long Id,
    Guid SessionId,
    long Sequence,
    string Direction,
    string MessageType,
    string Payload,
    DateTimeOffset ObservedAtUtc);

public sealed record HistoryPage<T>(
    IReadOnlyList<T> Items,
    int Skip,
    int Take,
    long Total);

public sealed record OperationsHistory(
    HistoryPage<JobHistoryItem> Jobs,
    HistoryPage<RunHistoryItem> Runs,
    HistoryPage<AlarmHistoryItem> Alarms,
    HistoryPage<ProtocolHistoryItem> ProtocolMessages);

public interface IOperationsHistoryQuery
{
    public Task<OperationsHistory> SearchAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
