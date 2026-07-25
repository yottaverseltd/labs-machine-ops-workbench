using Yottaverse.MachineOps.Core.GCode;

namespace Yottaverse.MachineOps.Core.Machines;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Faulted,
}

public enum OperatingStatus
{
    Unknown,
    Idle,
    Running,
    Paused,
    Alarm,
}

public sealed record MachineSnapshot(
    Guid MachineId,
    ConnectionStatus ConnectionStatus,
    OperatingStatus OperatingStatus,
    Position3D Position,
    double? FeedRate,
    double? SpindleSpeed,
    double Progress,
    int LastAcknowledgedCommand,
    long Sequence,
    string? LastError,
    DateTimeOffset ObservedAtUtc)
{
    public static MachineSnapshot Disconnected(Guid machineId, DateTimeOffset observedAtUtc) =>
        new(
            machineId,
            ConnectionStatus.Disconnected,
            OperatingStatus.Unknown,
            Position3D.Origin,
            null,
            null,
            0,
            0,
            0,
            null,
            observedAtUtc);
}
