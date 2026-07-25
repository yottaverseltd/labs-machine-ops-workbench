using System.Net;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Machines;
using Yottaverse.MachineOps.Infrastructure.Controller;
using Yottaverse.MachineOps.Simulator;

namespace Yottaverse.MachineOps.Simulator.Tests;

public sealed class TcpControllerSessionTests
{
    [Fact]
    public async Task ConnectRefreshAndDisconnectUsesTheWireProtocol()
    {
        await using SimulatorServer server = await StartServerAsync(SimulatorScenario.Normal);
        await using TcpControllerSession session = CreateSession();
        Guid machineId = Guid.NewGuid();

        MachineSnapshot connected = await session.ConnectAsync(
            ConnectionOptions(machineId, server.BoundPort),
            CancellationToken.None);
        MachineSnapshot refreshed = await session.RefreshAsync(
            CancellationToken.None);
        await session.DisconnectAsync(CancellationToken.None);

        Assert.Equal(machineId, connected.MachineId);
        Assert.Equal(ConnectionStatus.Connected, connected.ConnectionStatus);
        Assert.Equal(OperatingStatus.Idle, connected.OperatingStatus);
        Assert.True(refreshed.Sequence > connected.Sequence);
        Assert.Equal(ConnectionStatus.Disconnected, session.Snapshot.ConnectionStatus);
    }

    [Fact]
    public async Task DuplicateStateDoesNotMoveTheSnapshotBackwards()
    {
        await using SimulatorServer server = await StartServerAsync(SimulatorScenario.Duplicate);
        await using TcpControllerSession session = CreateSession();
        await session.ConnectAsync(
            ConnectionOptions(Guid.NewGuid(), server.BoundPort),
            CancellationToken.None);

        MachineSnapshot refreshed = await session.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(2, refreshed.Sequence);
        Assert.Equal(2, session.Snapshot.Sequence);
        await session.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MalformedStateFaultsTheSession()
    {
        await using SimulatorServer server = await StartServerAsync(SimulatorScenario.Malformed);
        await using TcpControllerSession session = CreateSession();
        await session.ConnectAsync(
            ConnectionOptions(Guid.NewGuid(), server.BoundPort),
            CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(
            () => session.RefreshAsync(CancellationToken.None));

        Assert.Equal(ConnectionStatus.Faulted, session.Snapshot.ConnectionStatus);
        Assert.Contains("JSON", session.Snapshot.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecutionCommandsDriveDeterministicProgress()
    {
        await using SimulatorServer server = await StartServerAsync(SimulatorScenario.Normal);
        await using TcpControllerSession session = CreateSession();
        await session.ConnectAsync(
            ConnectionOptions(Guid.NewGuid(), server.BoundPort),
            CancellationToken.None);

        MachineSnapshot started = await session.ExecuteAsync(
            ControllerOperation.Start,
            CancellationToken.None);
        MachineSnapshot firstSample = await session.RefreshAsync(CancellationToken.None);
        MachineSnapshot paused = await session.ExecuteAsync(
            ControllerOperation.Pause,
            CancellationToken.None);
        MachineSnapshot heldSample = await session.RefreshAsync(CancellationToken.None);
        MachineSnapshot resumed = await session.ExecuteAsync(
            ControllerOperation.Resume,
            CancellationToken.None);
        MachineSnapshot cancelled = await session.ExecuteAsync(
            ControllerOperation.Cancel,
            CancellationToken.None);

        Assert.Equal(OperatingStatus.Running, started.OperatingStatus);
        Assert.Equal(10, firstSample.Progress);
        Assert.Equal(OperatingStatus.Paused, paused.OperatingStatus);
        Assert.Equal(firstSample.Progress, heldSample.Progress);
        Assert.Equal(OperatingStatus.Running, resumed.OperatingStatus);
        Assert.Equal(OperatingStatus.Idle, cancelled.OperatingStatus);
    }

    [Fact]
    public async Task AlarmScenarioRaisesAControllerAlarm()
    {
        await using SimulatorServer server = await StartServerAsync(SimulatorScenario.Alarm);
        await using TcpControllerSession session = CreateSession();
        TaskCompletionSource<ControllerAlarmEventArgs> alarmReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.AlarmRaised += (_, eventArgs) => alarmReceived.TrySetResult(eventArgs);

        await session.ConnectAsync(
            ConnectionOptions(Guid.NewGuid(), server.BoundPort),
            CancellationToken.None);
        ControllerAlarmEventArgs alarm = await alarmReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal("E_STOP", alarm.Code);
        Assert.Contains("Emergency stop", alarm.Message, StringComparison.Ordinal);
    }

    private static ControllerConnectionOptions ConnectionOptions(Guid machineId, int port) =>
        new(machineId, "127.0.0.1", port, TimeSpan.FromSeconds(2));

    private static TcpControllerSession CreateSession() =>
        new(TimeProvider.System, new RecordingAuditStore());

    private static async Task<SimulatorServer> StartServerAsync(SimulatorScenario scenario)
    {
        SimulatorServer server = new(new SimulatorOptions(0, scenario, IPAddress.Loopback));
        await server.StartAsync();
        return server;
    }

    private sealed class RecordingAuditStore : IControllerAuditStore
    {
        private Guid sessionId;

        public Task<Guid> BeginSessionAsync(
            Guid machineId,
            string host,
            int port,
            DateTimeOffset connectedAtUtc,
            CancellationToken cancellationToken)
        {
            sessionId = Guid.NewGuid();
            return Task.FromResult(sessionId);
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
            Assert.Equal(this.sessionId, sessionId);
            return Task.CompletedTask;
        }

        public Task EndSessionAsync(
            Guid sessionId,
            string? reason,
            DateTimeOffset disconnectedAtUtc,
            CancellationToken cancellationToken)
        {
            Assert.Equal(this.sessionId, sessionId);
            return Task.CompletedTask;
        }
    }
}
