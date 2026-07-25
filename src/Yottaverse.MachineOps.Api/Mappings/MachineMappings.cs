using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Api.Mappings;

internal static class MachineMappings
{
    public static MachineSnapshotDto ToDto(this MachineSnapshot snapshot) =>
        new(
            snapshot.MachineId,
            snapshot.ConnectionStatus.ToString(),
            snapshot.OperatingStatus.ToString(),
            snapshot.Position.X,
            snapshot.Position.Y,
            snapshot.Position.Z,
            snapshot.FeedRate,
            snapshot.SpindleSpeed,
            snapshot.Progress,
            snapshot.LastAcknowledgedCommand,
            snapshot.Sequence,
            snapshot.LastError,
            snapshot.ObservedAtUtc);
}
