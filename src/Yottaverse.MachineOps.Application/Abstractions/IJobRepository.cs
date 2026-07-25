using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Application.Abstractions;

public interface IJobRepository
{
    public Task AddAsync(MachiningJob job, CancellationToken cancellationToken);

    public Task<MachiningJob?> GetAsync(Guid id, CancellationToken cancellationToken);

    public Task<IReadOnlyList<MachiningJob>> ListAsync(CancellationToken cancellationToken);
}
