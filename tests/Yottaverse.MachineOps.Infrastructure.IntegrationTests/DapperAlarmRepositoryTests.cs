using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Alarms;
using Yottaverse.MachineOps.Core.Machines;
using Yottaverse.MachineOps.Infrastructure.Database;

namespace Yottaverse.MachineOps.Infrastructure.IntegrationTests;

[Collection(PostgresTestGroup.Name)]
public sealed class DapperAlarmRepositoryTests
{
    private readonly PostgresFixture fixture;

    public DapperAlarmRepositoryTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task AlarmAcknowledgementAndOutboxCommitTogether()
    {
        DapperControllerAuditStore auditStore = new(fixture.DataSource);
        Guid sessionId = await auditStore.BeginSessionAsync(
            MachineIdentifiers.LocalSimulator,
            "127.0.0.1",
            5099,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        DapperAlarmRepository repository = new(fixture.DataSource);
        MachineAlarm alarm = MachineAlarm.Raise(
            Guid.NewGuid(),
            MachineIdentifiers.LocalSimulator,
            null,
            $"integration-{Guid.NewGuid():N}",
            "E_STOP",
            AlarmSeverity.Critical,
            "Emergency stop input is active.",
            DateTimeOffset.UtcNow);
        await repository.AddAsync(alarm, CancellationToken.None);
        AlarmAcknowledgement acknowledgement = alarm.Acknowledge(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "integration-test",
            "Input inspected",
            0,
            DateTimeOffset.UtcNow);

        await repository.SaveAcknowledgementAsync(
            alarm,
            acknowledgement,
            0,
            CancellationToken.None);
        MachineAlarm? readBack = await repository.GetAsync(alarm.Id, CancellationToken.None);
        DapperOutboxStore outbox = new(fixture.DataSource);
        IReadOnlyList<ClaimedOutboxMessage> messages =
            await outbox.ClaimAsync(20, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotNull(readBack);
        Assert.True(readBack.IsAcknowledged);
        Assert.Equal(1, readBack.Version);
        Assert.Contains(messages, message => message.MessageType == "alarm.raised");
        Assert.Contains(messages, message => message.MessageType == "alarm.acknowledged");
        await auditStore.EndSessionAsync(
            sessionId,
            null,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }

    [Fact]
    public async Task ExternalAlarmKeyIsIdempotent()
    {
        DapperControllerAuditStore auditStore = new(fixture.DataSource);
        Guid sessionId = await auditStore.BeginSessionAsync(
            MachineIdentifiers.LocalSimulator,
            "127.0.0.1",
            5099,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        DapperAlarmRepository repository = new(fixture.DataSource);
        string externalKey = $"duplicate-{Guid.NewGuid():N}";
        MachineAlarm first = CreateAlarm(externalKey);
        MachineAlarm duplicate = CreateAlarm(externalKey);

        bool firstAdded = await repository.AddAsync(first, CancellationToken.None);
        bool duplicateAdded = await repository.AddAsync(duplicate, CancellationToken.None);

        Assert.True(firstAdded);
        Assert.False(duplicateAdded);
        await auditStore.EndSessionAsync(
            sessionId,
            null,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }

    private static MachineAlarm CreateAlarm(string externalKey) =>
        MachineAlarm.Raise(
            Guid.NewGuid(),
            MachineIdentifiers.LocalSimulator,
            null,
            externalKey,
            "LIMIT",
            AlarmSeverity.Warning,
            "A limit input is active.",
            DateTimeOffset.UtcNow);
}
