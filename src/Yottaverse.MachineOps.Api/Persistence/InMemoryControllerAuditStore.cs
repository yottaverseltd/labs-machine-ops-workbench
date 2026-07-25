using System.Collections.Concurrent;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryControllerAuditStore : IControllerAuditStore
{
    private readonly ConcurrentDictionary<Guid, byte> sessions = new();

    public Task<Guid> BeginSessionAsync(
        Guid machineId,
        string host,
        int port,
        DateTimeOffset connectedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guid id = Guid.NewGuid();
        sessions.TryAdd(id, 0);
        return Task.FromResult(id);
    }

    public Task RecordAsync(
        Guid sessionId,
        long sequence,
        string direction,
        string messageType,
        string payload,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sessions.ContainsKey(sessionId))
        {
            throw new InvalidOperationException("The controller session has not been registered.");
        }

        return Task.CompletedTask;
    }

    public Task EndSessionAsync(
        Guid sessionId,
        string? reason,
        DateTimeOffset disconnectedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
