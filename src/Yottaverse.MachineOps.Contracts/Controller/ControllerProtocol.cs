using System.Text.Json;

namespace Yottaverse.MachineOps.Contracts.Controller;

public static class ControllerMessageTypes
{
    public const string Hello = "hello";
    public const string HelloAccepted = "hello_accepted";
    public const string GetState = "get_state";
    public const string State = "state";
    public const string Start = "start";
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Cancel = "cancel";
    public const string Disconnect = "disconnect";
    public const string CommandAccepted = "command_accepted";
    public const string ProtocolError = "protocol_error";
    public const string Alarm = "alarm";
}

public sealed record ControllerCommandMessage(
    string Type,
    Guid CorrelationId,
    int ProtocolVersion = 1,
    string? Payload = null);

public sealed record ControllerStateWire(
    string OperatingState,
    double X,
    double Y,
    double Z,
    double? FeedRate,
    double? SpindleSpeed,
    double Progress,
    int LastAcknowledgedCommand);

public sealed record ControllerEventMessage(
    string Type,
    Guid? CorrelationId,
    long Sequence,
    ControllerStateWire? State = null,
    string? Error = null,
    string? AlarmCode = null);

public static class ControllerProtocolJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(ControllerCommandMessage message) =>
        JsonSerializer.Serialize(message, Options);

    public static string Serialize(ControllerEventMessage message) =>
        JsonSerializer.Serialize(message, Options);

    public static ControllerCommandMessage? DeserializeCommand(string json) =>
        JsonSerializer.Deserialize<ControllerCommandMessage>(json, Options);

    public static ControllerEventMessage? DeserializeEvent(string json) =>
        JsonSerializer.Deserialize<ControllerEventMessage>(json, Options);
}
