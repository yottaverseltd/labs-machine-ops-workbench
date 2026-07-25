using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Alarms;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Application.Alarms;

public sealed class AlarmService
{
    private readonly IAlarmRepository alarmRepository;
    private readonly TimeProvider timeProvider;

    public AlarmService(IAlarmRepository alarmRepository, TimeProvider timeProvider)
    {
        this.alarmRepository = alarmRepository;
        this.timeProvider = timeProvider;
    }

    public async Task<MachineAlarm> RaiseAsync(
        string externalKey,
        string code,
        string message,
        Guid? jobRunId,
        CancellationToken cancellationToken)
    {
        MachineAlarm alarm = MachineAlarm.Raise(
            Guid.NewGuid(),
            MachineIdentifiers.LocalSimulator,
            jobRunId,
            externalKey,
            code,
            AlarmSeverity.Critical,
            message,
            timeProvider.GetUtcNow());
        bool added = await alarmRepository.AddAsync(alarm, cancellationToken);
        if (added)
        {
            return alarm;
        }

        IReadOnlyList<MachineAlarm> recent =
            await alarmRepository.ListAsync(0, 100, cancellationToken);
        return recent.Single(existing =>
            string.Equals(existing.ExternalKey, externalKey, StringComparison.Ordinal));
    }

    public Task<IReadOnlyList<MachineAlarm>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        alarmRepository.ListAsync(skip, Math.Clamp(take, 1, 100), cancellationToken);

    public async Task<MachineAlarm> AcknowledgeAsync(
        Guid alarmId,
        Guid idempotencyKey,
        string acknowledgedBy,
        string? note,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        MachineAlarm alarm = await alarmRepository.GetAsync(alarmId, cancellationToken)
            ?? throw new KeyNotFoundException($"Alarm '{alarmId}' was not found.");
        int originalVersion = alarm.Version;
        AlarmAcknowledgement acknowledgement = alarm.Acknowledge(
            Guid.NewGuid(),
            idempotencyKey,
            acknowledgedBy,
            note,
            expectedVersion,
            timeProvider.GetUtcNow());
        if (alarm.Version != originalVersion)
        {
            await alarmRepository.SaveAcknowledgementAsync(
                alarm,
                acknowledgement,
                originalVersion,
                cancellationToken);
        }

        return alarm;
    }
}
