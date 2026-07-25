using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Core.Tests;

public sealed class JobRunTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 7, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RunMovesThroughStartPauseResumeAndCompletion()
    {
        JobRun run = CreateRun();

        run.Start(StartTime);
        run.Pause();
        run.Resume();
        run.ObserveProgress(100, 12, StartTime.AddMinutes(1));

        Assert.Equal(JobRunState.Completed, run.State);
        Assert.Equal(12, run.LastCommandIndex);
        Assert.Equal(StartTime.AddMinutes(1), run.FinishedAtUtc);
    }

    [Fact]
    public void PausingAReadyRunIsRejected()
    {
        JobRun run = CreateRun();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(run.Pause);

        Assert.Contains("Ready", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OlderAcknowledgementDoesNotRegressProgress()
    {
        JobRun run = CreateRun();
        run.Start(StartTime);
        run.ObserveProgress(40, 8, StartTime.AddSeconds(1));

        run.ObserveProgress(20, 7, StartTime.AddSeconds(2));

        Assert.Equal(8, run.LastCommandIndex);
        Assert.Equal(JobRunState.Running, run.State);
    }

    [Fact]
    public void RunningOrPausedRunCanBeCancelled()
    {
        JobRun run = CreateRun();
        run.Start(StartTime);
        run.Pause();

        run.Cancel(StartTime.AddSeconds(10));

        Assert.Equal(JobRunState.Cancelled, run.State);
        Assert.Equal(StartTime.AddSeconds(10), run.FinishedAtUtc);
    }

    private static JobRun CreateRun() =>
        JobRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
