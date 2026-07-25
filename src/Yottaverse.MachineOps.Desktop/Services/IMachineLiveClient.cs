using Yottaverse.MachineOps.Contracts.Machines;

namespace Yottaverse.MachineOps.Desktop.Services;

public sealed class LiveSnapshotEventArgs : EventArgs
{
    public LiveSnapshotEventArgs(MachineSnapshotDto snapshot)
    {
        Snapshot = snapshot;
    }

    public MachineSnapshotDto Snapshot { get; }
}

public sealed class LiveConnectionStateEventArgs : EventArgs
{
    public LiveConnectionStateEventArgs(string state)
    {
        State = state;
    }

    public string State { get; }
}

public interface IMachineLiveClient : IAsyncDisposable
{
    public event EventHandler<LiveSnapshotEventArgs>? SnapshotReceived;

    public event EventHandler<LiveConnectionStateEventArgs>? ConnectionStateChanged;

    public Task StartAsync(CancellationToken cancellationToken);
}
