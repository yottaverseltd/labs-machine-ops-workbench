using System.Collections.Concurrent;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryAlarmRepository : IAlarmRepository
{
    private readonly ConcurrentDictionary<Guid, MachineAlarm> alarms = new();

    public Task<bool> AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool duplicate = alarms.Values.Any(existing =>
            existing.MachineId == alarm.MachineId &&
            string.Equals(existing.ExternalKey, alarm.ExternalKey, StringComparison.Ordinal));
        return Task.FromResult(!duplicate && alarms.TryAdd(alarm.Id, alarm));
    }

    public Task<MachineAlarm?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        alarms.TryGetValue(id, out MachineAlarm? alarm);
        return Task.FromResult(alarm);
    }

    public Task<IReadOnlyList<MachineAlarm>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MachineAlarm> result = alarms.Values
            .OrderByDescending(alarm => alarm.RaisedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task SaveAcknowledgementAsync(
        MachineAlarm alarm,
        AlarmAcknowledgement acknowledgement,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (alarm.Version != expectedVersion + 1)
        {
            throw new AlarmConcurrencyException(expectedVersion, alarm.Version);
        }

        alarms[alarm.Id] = alarm;
        return Task.CompletedTask;
    }
}
