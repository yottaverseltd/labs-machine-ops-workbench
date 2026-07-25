using Microsoft.AspNetCore.SignalR.Client;
using Yottaverse.MachineOps.Contracts.Live;
using Yottaverse.MachineOps.Contracts.Machines;

namespace Yottaverse.MachineOps.Desktop.Services;

public sealed class MachineLiveClient : IMachineLiveClient
{
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly IMachineOpsApiClient apiClient;
    private readonly HubConnection connection;
    private readonly IDisposable snapshotSubscription;

    public MachineLiveClient(Uri apiAddress, IMachineOpsApiClient apiClient)
    {
        this.apiClient = apiClient;
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(apiAddress, "/hubs/machines"))
            .WithAutomaticReconnect(
                [
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                ])
            .Build();
        snapshotSubscription = connection.On<MachineSnapshotDto>(
            MachineLiveEventNames.SnapshotChanged,
            snapshot => RaiseSnapshot(snapshot));
        connection.Reconnecting += _ =>
        {
            RaiseConnectionState("Reconnecting");
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            await ResynchroniseAsync(CancellationToken.None);
        };
        connection.Closed += _ =>
        {
            RaiseConnectionState("Unavailable");
            return Task.CompletedTask;
        };
    }

    public event EventHandler<LiveSnapshotEventArgs>? SnapshotReceived;

    public event EventHandler<LiveConnectionStateEventArgs>? ConnectionStateChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);
        try
        {
            if (connection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            RaiseConnectionState("Connecting");
            await connection.StartAsync(cancellationToken);
            await ResynchroniseAsync(cancellationToken);
        }
        finally
        {
            startGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        snapshotSubscription.Dispose();
        await connection.DisposeAsync();
        startGate.Dispose();
    }

    private async Task ResynchroniseAsync(CancellationToken cancellationToken)
    {
        MachineSnapshotDto snapshot = await apiClient.GetMachineSnapshotAsync(
            false,
            cancellationToken);
        RaiseSnapshot(snapshot);
        RaiseConnectionState("Live");
    }

    private void RaiseSnapshot(MachineSnapshotDto snapshot) =>
        SnapshotReceived?.Invoke(this, new LiveSnapshotEventArgs(snapshot));

    private void RaiseConnectionState(string state) =>
        ConnectionStateChanged?.Invoke(this, new LiveConnectionStateEventArgs(state));
}
