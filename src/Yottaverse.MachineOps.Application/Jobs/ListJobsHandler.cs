using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Application.Jobs;

public sealed class ListJobsHandler
{
    private readonly IJobRepository repository;

    public ListJobsHandler(IJobRepository repository)
    {
        this.repository = repository;
    }

    public Task<IReadOnlyList<MachiningJob>> HandleAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}
