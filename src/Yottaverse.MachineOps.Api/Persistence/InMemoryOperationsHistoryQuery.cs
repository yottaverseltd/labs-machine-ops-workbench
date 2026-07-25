using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryOperationsHistoryQuery : IOperationsHistoryQuery
{
    public Task<OperationsHistory> SearchAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new OperationsHistory(
                Empty<JobHistoryItem>(skip, take),
                Empty<RunHistoryItem>(skip, take),
                Empty<AlarmHistoryItem>(skip, take),
                Empty<ProtocolHistoryItem>(skip, take)));
    }

    private static HistoryPage<T> Empty<T>(int skip, int take) =>
        new([], skip, take, 0);
}
