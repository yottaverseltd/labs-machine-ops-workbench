using System.Text.Json;
using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperAlarmRepository : IAlarmRepository
{
    private const string SelectSql =
        """
        SELECT
            a.id AS Id,
            a.machine_id AS MachineId,
            a.job_run_id AS JobRunId,
            a.external_key AS ExternalKey,
            a.code AS Code,
            a.severity AS Severity,
            a.message AS Message,
            a.raised_at_utc AS RaisedAtUtc,
            a.version AS Version,
            ack.id AS AcknowledgementId,
            ack.idempotency_key AS IdempotencyKey,
            ack.acknowledged_by AS AcknowledgedBy,
            ack.acknowledged_at_utc AS AcknowledgedAtUtc,
            ack.note AS Note,
            ack.alarm_version AS AlarmVersion
        FROM alarms a
        LEFT JOIN LATERAL
        (
            SELECT *
            FROM alarm_acknowledgements
            WHERE alarm_id = a.id
            ORDER BY acknowledged_at_utc DESC
            LIMIT 1
        ) ack ON TRUE
        """;

    private readonly NpgsqlDataSource dataSource;

    public DapperAlarmRepository(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<bool> AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        const string insertAlarmSql =
            """
            INSERT INTO alarms
            (
                id, machine_id, job_run_id, external_key, code, severity,
                message, raised_at_utc, version
            )
            VALUES
            (
                @Id, @MachineId, @JobRunId, @ExternalKey, @Code, @Severity,
                @Message, @RaisedAtUtc, @Version
            )
            ON CONFLICT (machine_id, external_key) DO NOTHING;
            """;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        int inserted = await connection.ExecuteAsync(new CommandDefinition(
            insertAlarmSql,
            new
            {
                alarm.Id,
                alarm.MachineId,
                alarm.JobRunId,
                alarm.ExternalKey,
                alarm.Code,
                Severity = alarm.Severity.ToString(),
                alarm.Message,
                alarm.RaisedAtUtc,
                alarm.Version,
            },
            transaction,
            cancellationToken: cancellationToken));
        if (inserted == 1)
        {
            await InsertOutboxAsync(
                connection,
                transaction,
                "alarm.raised",
                alarm,
                alarm.RaisedAtUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inserted == 1;
    }

    public async Task<MachineAlarm?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        string sql = $"{SelectSql} WHERE a.id = @Id;";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        AlarmRow? row = await connection.QuerySingleOrDefaultAsync<AlarmRow>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<MachineAlarm>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        string sql =
            $"{SelectSql} ORDER BY a.raised_at_utc DESC OFFSET @Skip LIMIT @Take;";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<AlarmRow> rows = await connection.QueryAsync<AlarmRow>(new CommandDefinition(
            sql,
            new { Skip = skip, Take = take },
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    public async Task SaveAcknowledgementAsync(
        MachineAlarm alarm,
        AlarmAcknowledgement acknowledgement,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        const string updateAlarmSql =
            """
            UPDATE alarms
            SET version = @Version
            WHERE id = @Id
              AND version = @ExpectedVersion;
            """;
        const string insertAcknowledgementSql =
            """
            INSERT INTO alarm_acknowledgements
            (
                id, alarm_id, idempotency_key, acknowledged_by,
                acknowledged_at_utc, note, alarm_version
            )
            VALUES
            (
                @Id, @AlarmId, @IdempotencyKey, @AcknowledgedBy,
                @AcknowledgedAtUtc, @Note, @AlarmVersion
            );
            """;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        int updated = await connection.ExecuteAsync(new CommandDefinition(
            updateAlarmSql,
            new
            {
                alarm.Id,
                alarm.Version,
                ExpectedVersion = expectedVersion,
            },
            transaction,
            cancellationToken: cancellationToken));
        if (updated != 1)
        {
            int actualVersion = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT version FROM alarms WHERE id = @Id;",
                new { alarm.Id },
                transaction,
                cancellationToken: cancellationToken));
            throw new AlarmConcurrencyException(expectedVersion, actualVersion);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            insertAcknowledgementSql,
            new
            {
                acknowledgement.Id,
                acknowledgement.AlarmId,
                acknowledgement.IdempotencyKey,
                acknowledgement.AcknowledgedBy,
                acknowledgement.AcknowledgedAtUtc,
                acknowledgement.Note,
                AlarmVersion = acknowledgement.AlarmVersion,
            },
            transaction,
            cancellationToken: cancellationToken));
        await InsertOutboxAsync(
            connection,
            transaction,
            "alarm.acknowledged",
            alarm,
            acknowledgement.AcknowledgedAtUtc,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string messageType,
        MachineAlarm alarm,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO outbox_messages
                (id, message_type, payload, occurred_at_utc)
            VALUES
                (@Id, @MessageType, CAST(@Payload AS jsonb), @OccurredAtUtc);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = Guid.NewGuid(),
                MessageType = messageType,
                Payload = JsonSerializer.Serialize(new AlarmNotificationDto(
                    alarm.Id,
                    alarm.MachineId,
                    alarm.Code,
                    alarm.Message,
                    alarm.Version,
                    alarm.IsAcknowledged,
                    alarm.RaisedAtUtc)),
                OccurredAtUtc = occurredAtUtc,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static MachineAlarm Map(AlarmRow row)
    {
        AlarmAcknowledgement? acknowledgement = row.AcknowledgementId is Guid acknowledgementId
            ? new AlarmAcknowledgement(
                acknowledgementId,
                row.Id,
                row.IdempotencyKey!.Value,
                row.AcknowledgedBy!,
                ToUtc(row.AcknowledgedAtUtc!.Value),
                row.Note,
                row.AlarmVersion!.Value)
            : null;
        return MachineAlarm.Rehydrate(
            row.Id,
            row.MachineId,
            row.JobRunId,
            row.ExternalKey,
            row.Code,
            Enum.Parse<AlarmSeverity>(row.Severity),
            row.Message,
            ToUtc(row.RaisedAtUtc),
            row.Version,
            acknowledgement);
    }

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record AlarmRow(
        Guid Id,
        Guid MachineId,
        Guid? JobRunId,
        string ExternalKey,
        string Code,
        string Severity,
        string Message,
        DateTime RaisedAtUtc,
        int Version,
        Guid? AcknowledgementId,
        Guid? IdempotencyKey,
        string? AcknowledgedBy,
        DateTime? AcknowledgedAtUtc,
        string? Note,
        int? AlarmVersion);
}
