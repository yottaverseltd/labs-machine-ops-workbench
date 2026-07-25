using Yottaverse.MachineOps.Contracts.Jobs;

namespace Yottaverse.MachineOps.Desktop.Services;

public interface IMachineOpsApiClient
{
    public Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    public Task<JobDto> CreateJobAsync(
        CreateJobRequest request,
        CancellationToken cancellationToken);
}
