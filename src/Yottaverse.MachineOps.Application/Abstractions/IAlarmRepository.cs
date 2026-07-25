using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Application.Abstractions;

public interface IAlarmRepository
{
    public Task<bool> AddAsync(MachineAlarm alarm, CancellationToken cancellationToken);

    public Task<MachineAlarm?> GetAsync(Guid id, CancellationToken cancellationToken);

    public Task<IReadOnlyList<MachineAlarm>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    public Task SaveAcknowledgementAsync(
        MachineAlarm alarm,
        AlarmAcknowledgement acknowledgement,
        int expectedVersion,
        CancellationToken cancellationToken);
}
