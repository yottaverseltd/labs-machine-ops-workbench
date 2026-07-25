using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Core.Tests;

public sealed class MachineAlarmTests
{
    [Fact]
    public void AcknowledgeAdvancesTheVersion()
    {
        MachineAlarm alarm = CreateAlarm();

        AlarmAcknowledgement acknowledgement = alarm.Acknowledge(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "operator",
            "Checked the input",
            0,
            DateTimeOffset.UtcNow);

        Assert.True(alarm.IsAcknowledged);
        Assert.Equal(1, alarm.Version);
        Assert.Equal("operator", acknowledgement.AcknowledgedBy);
    }

    [Fact]
    public void RepeatingAnIdempotencyKeyReturnsTheOriginalAcknowledgement()
    {
        MachineAlarm alarm = CreateAlarm();
        Guid key = Guid.NewGuid();
        AlarmAcknowledgement first = alarm.Acknowledge(
            Guid.NewGuid(),
            key,
            "operator",
            null,
            0,
            DateTimeOffset.UtcNow);

        AlarmAcknowledgement repeated = alarm.Acknowledge(
            Guid.NewGuid(),
            key,
            "someone else",
            "This payload is ignored",
            0,
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Same(first, repeated);
        Assert.Equal(1, alarm.Version);
    }

    [Fact]
    public void StaleVersionIsRejected()
    {
        MachineAlarm alarm = CreateAlarm();

        Assert.Throws<AlarmConcurrencyException>(
            () => alarm.Acknowledge(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "operator",
                null,
                4,
                DateTimeOffset.UtcNow));
    }

    private static MachineAlarm CreateAlarm() =>
        MachineAlarm.Raise(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "session-12-sequence-4",
            "E_STOP",
            AlarmSeverity.Critical,
            "Emergency stop input is active.",
            DateTimeOffset.UtcNow);
}
