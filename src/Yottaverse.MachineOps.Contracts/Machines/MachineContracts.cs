using System.ComponentModel.DataAnnotations;

namespace Yottaverse.MachineOps.Contracts.Machines;

public sealed record ConnectSimulatorRequest(
    [Range(1, 65535)] int Port = 5099);

public sealed record MachineSnapshotDto(
    Guid MachineId,
    string ConnectionStatus,
    string OperatingStatus,
    double X,
    double Y,
    double Z,
    double? FeedRate,
    double? SpindleSpeed,
    double Progress,
    int LastAcknowledgedCommand,
    long Sequence,
    string? LastError,
    DateTimeOffset ObservedAtUtc);
