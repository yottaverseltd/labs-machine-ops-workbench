using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Runs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;
using Yottaverse.MachineOps.Core.Machines;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Application.Tests;

public sealed class RunCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AcceptedControllerOperationsChangeAndPersistRunState()
    {
        TestContext context = CreateContext();
        using RunCoordinator coordinator = context.Coordinator;

        await coordinator.StartAsync(context.Job.Id, CancellationToken.None);
        JobRun paused = await coordinator.PauseAsync(CancellationToken.None);
        JobRun resumed = await coordinator.ResumeAsync(CancellationToken.None);
        JobRun cancelled = await coordinator.CancelAsync(CancellationToken.None);

        Assert.Equal(JobRunState.Cancelled, cancelled.State);
        Assert.Same(paused, resumed);
        Assert.Same(resumed, cancelled);
        Assert.Equal(
            [
                ControllerOperation.Pause,
                ControllerOperation.Resume,
                ControllerOperation.Cancel,
            ],
            context.Controller.Operations);
        Assert.Equal(1, context.Controller.StartCount);
        Assert.Equal(
            [
                JobRunState.Running,
                JobRunState.Paused,
                JobRunState.Running,
                JobRunState.Cancelled,
            ],
            context.Runs.SavedStates);
        Assert.Equal(context.Job.Program.Segments, context.Controller.StartedToolpath);
    }

    [Fact]
    public async Task RejectedControllerOperationFailsTheRunAndKeepsTheOriginalError()
    {
        TestContext context = CreateContext();
        using RunCoordinator coordinator = context.Coordinator;
        await coordinator.StartAsync(context.Job.Id, CancellationToken.None);
        context.Controller.RejectedOperation = ControllerOperation.Cancel;

        IOException error = await Assert.ThrowsAsync<IOException>(
            () => coordinator.CancelAsync(CancellationToken.None));

        Assert.Equal("Controller rejected Cancel.", error.Message);
        Assert.Equal(JobRunState.Failed, coordinator.ActiveRun?.State);
        Assert.Equal(
            "Controller did not accept Cancel.",
            coordinator.ActiveRun?.FailureReason);
        Assert.Equal(JobRunState.Failed, context.Runs.SavedStates[^1]);
    }

    [Fact]
    public async Task InvalidTransitionIsRejectedBeforeSendingAControllerCommand()
    {
        TestContext context = CreateContext();
        using RunCoordinator coordinator = context.Coordinator;
        await coordinator.StartAsync(context.Job.Id, CancellationToken.None);

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.ResumeAsync(CancellationToken.None));

        Assert.Contains("cannot resume", error.Message, StringComparison.Ordinal);
        Assert.Empty(context.Controller.Operations);
        Assert.Equal(1, context.Controller.StartCount);
        Assert.Equal(JobRunState.Running, coordinator.ActiveRun?.State);
    }

    private static TestContext CreateContext()
    {
        ParsedGCodeProgram program = new GCodeParser().Parse(
            "test.nc",
            "G21 G90\nG0 X0 Y0\nG1 X10 Y10 F300");
        MachiningJob job = MachiningJob.Create(
            Guid.NewGuid(),
            "Run test",
            program,
            Now);
        RecordingControllerSession controller = new(Now);
        RecordingRunRepository runs = new();
        RunCoordinator coordinator = new(
            controller,
            new SingleJobRepository(job),
            runs,
            new StubTimeProvider(Now));
        return new TestContext(job, controller, runs, coordinator);
    }

    private sealed record TestContext(
        MachiningJob Job,
        RecordingControllerSession Controller,
        RecordingRunRepository Runs,
        RunCoordinator Coordinator);

    private sealed class RecordingControllerSession : IControllerSession
    {
        public RecordingControllerSession(DateTimeOffset now)
        {
            Snapshot = new MachineSnapshot(
                MachineIdentifiers.LocalSimulator,
                ConnectionStatus.Connected,
                OperatingStatus.Idle,
                Position3D.Origin,
                null,
                null,
                0,
                0,
                1,
                null,
                now);
        }

        public event EventHandler<MachineSnapshotChangedEventArgs>? SnapshotChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ControllerAlarmEventArgs>? AlarmRaised
        {
            add { }
            remove { }
        }

        public MachineSnapshot Snapshot { get; private set; }

        public List<ControllerOperation> Operations { get; } = [];

        public IReadOnlyList<ToolpathSegment>? StartedToolpath { get; private set; }

        public int StartCount { get; private set; }

        public ControllerOperation? RejectedOperation { get; set; }

        public Task<MachineSnapshot> ConnectAsync(
            ControllerConnectionOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task<MachineSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task<MachineSnapshot> StartAsync(
            IReadOnlyList<ToolpathSegment> toolpath,
            CancellationToken cancellationToken)
        {
            StartCount++;
            StartedToolpath = toolpath;
            return Task.FromResult(Snapshot);
        }

        public Task<MachineSnapshot> ExecuteAsync(
            ControllerOperation operation,
            CancellationToken cancellationToken)
        {
            Operations.Add(operation);
            if (operation == RejectedOperation)
            {
                throw new IOException($"Controller rejected {operation}.");
            }

            return Task.FromResult(Snapshot);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SingleJobRepository : IJobRepository
    {
        private readonly MachiningJob job;

        public SingleJobRepository(MachiningJob job)
        {
            this.job = job;
        }

        public Task AddAsync(MachiningJob job, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MachiningJob?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<MachiningJob?>(id == job.Id ? job : null);

        public Task<IReadOnlyList<MachiningJob>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MachiningJob>>([job]);
    }

    private sealed class RecordingRunRepository : IRunRepository
    {
        public List<JobRunState> SavedStates { get; } = [];

        public Task SaveAsync(JobRun run, CancellationToken cancellationToken)
        {
            SavedStates.Add(run.State);
            return Task.CompletedTask;
        }
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public StubTimeProvider(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow() => now;
    }
}
