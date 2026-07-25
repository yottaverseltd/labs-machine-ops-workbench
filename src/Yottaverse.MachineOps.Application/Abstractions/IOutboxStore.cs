namespace Yottaverse.MachineOps.Application.Abstractions;

public sealed record ClaimedOutboxMessage(
    Guid Id,
    string MessageType,
    string Payload);

public interface IOutboxStore
{
    public Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(
        int batchSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    public Task MarkProcessedAsync(
        Guid id,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken);

    public Task MarkFailedAsync(
        Guid id,
        string failure,
        CancellationToken cancellationToken);
}
