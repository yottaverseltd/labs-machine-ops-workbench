using System.Collections.Concurrent;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, MachiningJob> jobs = new();

    public Task AddAsync(MachiningJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        if (!jobs.TryAdd(job.Id, job))
        {
            throw new InvalidOperationException($"Job {job.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<MachiningJob?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        jobs.TryGetValue(id, out MachiningJob? job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<MachiningJob>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MachiningJob> result = jobs.Values
            .OrderByDescending(job => job.CreatedAtUtc)
            .ToArray();
        return Task.FromResult(result);
    }
}
