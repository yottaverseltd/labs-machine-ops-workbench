using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Application.Abstractions;

public sealed record ControllerConnectionOptions(
    Guid MachineId,
    string Host,
    int Port,
    TimeSpan Timeout);

public enum ControllerOperation
{
    Start,
    Pause,
    Resume,
    Cancel,
}

public sealed class MachineSnapshotChangedEventArgs : EventArgs
{
    public MachineSnapshotChangedEventArgs(MachineSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public MachineSnapshot Snapshot { get; }
}

public interface IControllerSession
{
    public event EventHandler<MachineSnapshotChangedEventArgs>? SnapshotChanged;

    public MachineSnapshot Snapshot { get; }

    public Task<MachineSnapshot> ConnectAsync(
        ControllerConnectionOptions options,
        CancellationToken cancellationToken);

    public Task<MachineSnapshot> RefreshAsync(CancellationToken cancellationToken);

    public Task<MachineSnapshot> ExecuteAsync(
        ControllerOperation operation,
        CancellationToken cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken);
}
