namespace Yottaverse.MachineOps.Core.Alarms;

public enum AlarmSeverity
{
    Information,
    Warning,
    Critical,
}

public sealed record AlarmAcknowledgement(
    Guid Id,
    Guid AlarmId,
    Guid IdempotencyKey,
    string AcknowledgedBy,
    DateTimeOffset AcknowledgedAtUtc,
    string? Note,
    int AlarmVersion);

public sealed class MachineAlarm
{
    private MachineAlarm(
        Guid id,
        Guid machineId,
        Guid? jobRunId,
        string externalKey,
        string code,
        AlarmSeverity severity,
        string message,
        DateTimeOffset raisedAtUtc,
        int version,
        AlarmAcknowledgement? acknowledgement)
    {
        Id = id;
        MachineId = machineId;
        JobRunId = jobRunId;
        ExternalKey = externalKey;
        Code = code;
        Severity = severity;
        Message = message;
        RaisedAtUtc = raisedAtUtc;
        Version = version;
        Acknowledgement = acknowledgement;
    }

    public Guid Id { get; }

    public Guid MachineId { get; }

    public Guid? JobRunId { get; }

    public string ExternalKey { get; }

    public string Code { get; }

    public AlarmSeverity Severity { get; }

    public string Message { get; }

    public DateTimeOffset RaisedAtUtc { get; }

    public int Version { get; private set; }

    public AlarmAcknowledgement? Acknowledgement { get; private set; }

    public bool IsAcknowledged => Acknowledgement is not null;

    public static MachineAlarm Raise(
        Guid id,
        Guid machineId,
        Guid? jobRunId,
        string externalKey,
        string code,
        AlarmSeverity severity,
        string message,
        DateTimeOffset raisedAtUtc)
    {
        if (id == Guid.Empty || machineId == Guid.Empty)
        {
            throw new ArgumentException("Alarm and machine identifiers are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(externalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new MachineAlarm(
            id,
            machineId,
            jobRunId,
            externalKey.Trim(),
            code.Trim(),
            severity,
            message.Trim(),
            raisedAtUtc,
            0,
            null);
    }

    public AlarmAcknowledgement Acknowledge(
        Guid acknowledgementId,
        Guid idempotencyKey,
        string acknowledgedBy,
        string? note,
        int expectedVersion,
        DateTimeOffset acknowledgedAtUtc)
    {
        if (Acknowledgement?.IdempotencyKey == idempotencyKey)
        {
            return Acknowledgement;
        }

        if (Acknowledgement is not null)
        {
            throw new InvalidOperationException("The alarm has already been acknowledged.");
        }

        if (expectedVersion != Version)
        {
            throw new AlarmConcurrencyException(expectedVersion, Version);
        }

        if (acknowledgementId == Guid.Empty || idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException("Acknowledgement and idempotency identifiers are required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgedBy);
        Version++;
        Acknowledgement = new AlarmAcknowledgement(
            acknowledgementId,
            Id,
            idempotencyKey,
            acknowledgedBy.Trim(),
            acknowledgedAtUtc,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Version);
        return Acknowledgement;
    }

    public static MachineAlarm Rehydrate(
        Guid id,
        Guid machineId,
        Guid? jobRunId,
        string externalKey,
        string code,
        AlarmSeverity severity,
        string message,
        DateTimeOffset raisedAtUtc,
        int version,
        AlarmAcknowledgement? acknowledgement) =>
        new(
            id,
            machineId,
            jobRunId,
            externalKey,
            code,
            severity,
            message,
            raisedAtUtc,
            version,
            acknowledgement);
}

public sealed class AlarmConcurrencyException : Exception
{
    public AlarmConcurrencyException(int expectedVersion, int actualVersion)
        : base($"Alarm version {expectedVersion} was stale. Current version is {actualVersion}.")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public int ExpectedVersion { get; }

    public int ActualVersion { get; }
}
