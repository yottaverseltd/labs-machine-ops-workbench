using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Api.Mappings;

internal static class AlarmMappings
{
    public static AlarmDto ToDto(this MachineAlarm alarm) =>
        new(
            alarm.Id,
            alarm.MachineId,
            alarm.JobRunId,
            alarm.Code,
            alarm.Severity.ToString(),
            alarm.Message,
            alarm.RaisedAtUtc,
            alarm.Version,
            alarm.IsAcknowledged,
            alarm.Acknowledgement?.AcknowledgedBy,
            alarm.Acknowledgement?.AcknowledgedAtUtc,
            alarm.Acknowledgement?.Note);
}
