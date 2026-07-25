namespace Yottaverse.MachineOps.Core.Runs;

public enum JobRunState
{
    Ready,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed,
}

public sealed class JobRun
{
    private JobRun(Guid id, Guid jobId, Guid machineId)
    {
        Id = id;
        JobId = jobId;
        MachineId = machineId;
        State = JobRunState.Ready;
    }

    public Guid Id { get; }

    public Guid JobId { get; }

    public Guid MachineId { get; }

    public JobRunState State { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public int LastCommandIndex { get; private set; }

    public string? FailureReason { get; private set; }

    public static JobRun Create(Guid id, Guid jobId, Guid machineId)
    {
        if (id == Guid.Empty || jobId == Guid.Empty || machineId == Guid.Empty)
        {
            throw new ArgumentException("Run, job, and machine identifiers are required.");
        }

        return new JobRun(id, jobId, machineId);
    }

    public void Start(DateTimeOffset startedAtUtc)
    {
        RequireState(JobRunState.Ready, "start");
        State = JobRunState.Running;
        StartedAtUtc = startedAtUtc;
    }

    public void Pause()
    {
        RequireState(JobRunState.Running, "pause");
        State = JobRunState.Paused;
    }

    public void Resume()
    {
        RequireState(JobRunState.Paused, "resume");
        State = JobRunState.Running;
    }

    public void Cancel(DateTimeOffset finishedAtUtc)
    {
        if (State is not (JobRunState.Running or JobRunState.Paused))
        {
            throw InvalidTransition("cancel");
        }

        State = JobRunState.Cancelled;
        FinishedAtUtc = finishedAtUtc;
    }

    public void ObserveProgress(double progress, int acknowledgedCommand, DateTimeOffset observedAtUtc)
    {
        if (progress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100.");
        }

        if (acknowledgedCommand < LastCommandIndex)
        {
            return;
        }

        LastCommandIndex = acknowledgedCommand;
        if (progress >= 100 && State == JobRunState.Running)
        {
            State = JobRunState.Completed;
            FinishedAtUtc = observedAtUtc;
        }
    }

    public void Fail(string reason, DateTimeOffset finishedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (State is JobRunState.Completed or JobRunState.Cancelled)
        {
            throw InvalidTransition("fail");
        }

        State = JobRunState.Failed;
        FailureReason = reason;
        FinishedAtUtc = finishedAtUtc;
    }

    private void RequireState(JobRunState required, string operation)
    {
        if (State != required)
        {
            throw InvalidTransition(operation);
        }
    }

    private InvalidOperationException InvalidTransition(string operation) =>
        new($"A run in state '{State}' cannot {operation}.");
}
