using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Yottaverse.MachineOps.Api.Mappings;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Contracts.Live;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Api.Live;

public sealed class MachineUpdateBroadcaster : BackgroundService
{
    private readonly IControllerSession controllerSession;
    private readonly IHubContext<MachineHub> hubContext;
    private readonly Channel<MachineSnapshot> updates =
        Channel.CreateBounded<MachineSnapshot>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

    public MachineUpdateBroadcaster(
        IControllerSession controllerSession,
        IHubContext<MachineHub> hubContext)
    {
        this.controllerSession = controllerSession;
        this.hubContext = hubContext;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        controllerSession.SnapshotChanged += OnSnapshotChanged;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        controllerSession.SnapshotChanged -= OnSnapshotChanged;
        updates.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (MachineSnapshot snapshot in updates.Reader.ReadAllAsync(stoppingToken))
        {
            MachineSnapshotDto dto = snapshot.ToDto();
            await hubContext.Clients.All.SendAsync(
                MachineLiveEventNames.SnapshotChanged,
                dto,
                stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }

    private void OnSnapshotChanged(object? sender, MachineSnapshotChangedEventArgs eventArgs) =>
        updates.Writer.TryWrite(eventArgs.Snapshot);
}
