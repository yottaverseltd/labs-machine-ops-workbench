using Yottaverse.MachineOps.Contracts.Runs;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Api.Mappings;

internal static class RunMappings
{
    public static JobRunDto ToDto(this JobRun run) =>
        new(
            run.Id,
            run.JobId,
            run.MachineId,
            run.State.ToString(),
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.LastCommandIndex,
            run.FailureReason);
}
