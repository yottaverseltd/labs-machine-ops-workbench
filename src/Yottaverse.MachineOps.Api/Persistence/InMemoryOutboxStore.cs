using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryOutboxStore : IOutboxStore
{
    public Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(
        int batchSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ClaimedOutboxMessage>>([]);

    public Task MarkProcessedAsync(
        Guid id,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MarkFailedAsync(
        Guid id,
        string failure,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
