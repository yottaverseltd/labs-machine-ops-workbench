namespace Yottaverse.MachineOps.Core.Jobs;

public enum JobState
{
    Draft,
    Queued,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed,
}
