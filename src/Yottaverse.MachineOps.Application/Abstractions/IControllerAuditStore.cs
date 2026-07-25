namespace Yottaverse.MachineOps.Application.Abstractions;

public interface IControllerAuditStore
{
    public Task<Guid> BeginSessionAsync(
        Guid machineId,
        string host,
        int port,
        DateTimeOffset connectedAtUtc,
        CancellationToken cancellationToken);

    public Task RecordAsync(
        Guid sessionId,
        long sequence,
        string direction,
        string messageType,
        string payload,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    public Task EndSessionAsync(
        Guid sessionId,
        string? reason,
        DateTimeOffset disconnectedAtUtc,
        CancellationToken cancellationToken);
}
