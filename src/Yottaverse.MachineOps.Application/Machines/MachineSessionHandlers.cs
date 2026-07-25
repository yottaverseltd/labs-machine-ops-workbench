using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Application.Machines;

public sealed class ConnectSimulatorHandler
{
    private readonly IControllerSession controllerSession;

    public ConnectSimulatorHandler(IControllerSession controllerSession)
    {
        this.controllerSession = controllerSession;
    }

    public Task<MachineSnapshot> HandleAsync(
        int port,
        CancellationToken cancellationToken)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        return controllerSession.ConnectAsync(
            new ControllerConnectionOptions(
                MachineIdentifiers.LocalSimulator,
                "127.0.0.1",
                port,
                TimeSpan.FromSeconds(5)),
            cancellationToken);
    }
}

public sealed class GetMachineSnapshotHandler
{
    private readonly IControllerSession controllerSession;

    public GetMachineSnapshotHandler(IControllerSession controllerSession)
    {
        this.controllerSession = controllerSession;
    }

    public Task<MachineSnapshot> HandleAsync(
        bool refresh,
        CancellationToken cancellationToken) =>
        refresh
            ? controllerSession.RefreshAsync(cancellationToken)
            : Task.FromResult(controllerSession.Snapshot);
}

public sealed class DisconnectMachineHandler
{
    private readonly IControllerSession controllerSession;

    public DisconnectMachineHandler(IControllerSession controllerSession)
    {
        this.controllerSession = controllerSession;
    }

    public Task HandleAsync(CancellationToken cancellationToken) =>
        controllerSession.DisconnectAsync(cancellationToken);
}
