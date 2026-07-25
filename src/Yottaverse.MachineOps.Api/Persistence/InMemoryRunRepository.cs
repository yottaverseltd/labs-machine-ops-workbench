using System.Collections.Concurrent;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryRunRepository : IRunRepository
{
    private readonly ConcurrentDictionary<Guid, JobRun> runs = new();

    public Task SaveAsync(JobRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        cancellationToken.ThrowIfCancellationRequested();
        runs[run.Id] = run;
        return Task.CompletedTask;
    }
}
