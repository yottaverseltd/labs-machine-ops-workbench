using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Contracts.History;
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

    public Task<IReadOnlyList<AlarmDto>> ListAlarmsAsync(CancellationToken cancellationToken);

    public Task<AlarmDto> AcknowledgeAlarmAsync(
        Guid alarmId,
        AcknowledgeAlarmRequest request,
        CancellationToken cancellationToken);

    public Task<OperationsHistoryDto> SearchHistoryAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
