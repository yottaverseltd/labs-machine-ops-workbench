using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Contracts.Runs;

namespace Yottaverse.MachineOps.Desktop.Services;

public interface IMachineOpsApiClient
{
    public Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    public Task<JobDto> CreateJobAsync(
        CreateJobRequest request,
        CancellationToken cancellationToken);

    public Task<MachineSnapshotDto> ConnectSimulatorAsync(
        int port,
        CancellationToken cancellationToken);

    public Task<MachineSnapshotDto> GetMachineSnapshotAsync(
        bool refresh,
        CancellationToken cancellationToken);

    public Task DisconnectSimulatorAsync(CancellationToken cancellationToken);

    public Task<JobRunDto> StartRunAsync(Guid jobId, CancellationToken cancellationToken);

    public Task<JobRunDto> RefreshRunAsync(CancellationToken cancellationToken);

    public Task<JobRunDto> SendRunCommandAsync(
        string command,
        CancellationToken cancellationToken);
}
