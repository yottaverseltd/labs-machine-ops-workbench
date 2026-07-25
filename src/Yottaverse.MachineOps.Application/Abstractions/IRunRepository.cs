using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Application.Abstractions;

public interface IRunRepository
{
    public Task SaveAsync(JobRun run, CancellationToken cancellationToken);
}
