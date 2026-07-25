using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Application.Jobs;

public sealed class GetJobHandler
{
    private readonly IJobRepository repository;

    public GetJobHandler(IJobRepository repository)
    {
        this.repository = repository;
    }

    public Task<MachiningJob?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);
}
