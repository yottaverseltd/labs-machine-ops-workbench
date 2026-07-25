using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperOperationsHistoryQuery : IOperationsHistoryQuery
{
    private const string SearchSql =
        """
        SELECT COUNT(*)
        FROM jobs
        WHERE @Pattern IS NULL
           OR name ILIKE @Pattern
           OR state ILIKE @Pattern;

        SELECT id, name, state, created_at_utc AS CreatedAtUtc
        FROM jobs
        WHERE @Pattern IS NULL
           OR name ILIKE @Pattern
           OR state ILIKE @Pattern
        ORDER BY created_at_utc DESC
        OFFSET @Skip LIMIT @Take;

        SELECT COUNT(*)
        FROM job_runs r
        JOIN jobs j ON j.id = r.job_id
        WHERE @Pattern IS NULL
           OR j.name ILIKE @Pattern
           OR r.state ILIKE @Pattern
           OR r.failure_reason ILIKE @Pattern;

        SELECT
            r.id,
            r.job_id AS JobId,
            j.name AS JobName,
            r.state,
            r.started_at_utc AS StartedAtUtc,
            r.finished_at_utc AS FinishedAtUtc,
            r.failure_reason AS FailureReason
        FROM job_runs r
        JOIN jobs j ON j.id = r.job_id
        WHERE @Pattern IS NULL
           OR j.name ILIKE @Pattern
           OR r.state ILIKE @Pattern
           OR r.failure_reason ILIKE @Pattern
        ORDER BY r.started_at_utc DESC NULLS LAST, r.id
        OFFSET @Skip LIMIT @Take;

        SELECT COUNT(*)
        FROM alarms
        WHERE @Pattern IS NULL
           OR code ILIKE @Pattern
           OR severity ILIKE @Pattern
           OR message ILIKE @Pattern;

        SELECT
            a.id,
            a.code,
            a.severity,
            a.message,
            a.raised_at_utc AS RaisedAtUtc,
            EXISTS
            (
                SELECT 1
                FROM alarm_acknowledgements ack
                WHERE ack.alarm_id = a.id
            ) AS IsAcknowledged
        FROM alarms a
        WHERE @Pattern IS NULL
           OR a.code ILIKE @Pattern
           OR a.severity ILIKE @Pattern
           OR a.message ILIKE @Pattern
        ORDER BY a.raised_at_utc DESC
        OFFSET @Skip LIMIT @Take;

        SELECT COUNT(*)
        FROM protocol_messages
        WHERE @Pattern IS NULL
           OR direction ILIKE @Pattern
           OR message_type ILIKE @Pattern
           OR payload ILIKE @Pattern;

        SELECT
            id,
            session_id AS SessionId,
            sequence,
            direction,
            message_type AS MessageType,
            payload,
            observed_at_utc AS ObservedAtUtc
        FROM protocol_messages
        WHERE @Pattern IS NULL
           OR direction ILIKE @Pattern
           OR message_type ILIKE @Pattern
           OR payload ILIKE @Pattern
        ORDER BY observed_at_utc DESC, id DESC
        OFFSET @Skip LIMIT @Take;
        """;

    private readonly NpgsqlDataSource dataSource;

    public DapperOperationsHistoryQuery(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<OperationsHistory> SearchAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        string? pattern = string.IsNullOrWhiteSpace(query)
            ? null
            : $"%{query.Trim()}%";
        await using NpgsqlConnection connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        using SqlMapper.GridReader results = await connection.QueryMultipleAsync(
            new CommandDefinition(
                SearchSql,
                new { Pattern = pattern, Skip = skip, Take = take },
                cancellationToken: cancellationToken));

        long jobTotal = await results.ReadSingleAsync<long>();
        JobHistoryItem[] jobs = (await results.ReadAsync<JobRow>())
            .Select(Map)
            .ToArray();
        long runTotal = await results.ReadSingleAsync<long>();
        RunHistoryItem[] runs = (await results.ReadAsync<RunRow>())
            .Select(Map)
            .ToArray();
        long alarmTotal = await results.ReadSingleAsync<long>();
        AlarmHistoryItem[] alarms = (await results.ReadAsync<AlarmRow>())
            .Select(Map)
            .ToArray();
        long protocolTotal = await results.ReadSingleAsync<long>();
        ProtocolHistoryItem[] protocol = (await results.ReadAsync<ProtocolRow>())
            .Select(Map)
            .ToArray();

        return new OperationsHistory(
            new HistoryPage<JobHistoryItem>(jobs, skip, take, jobTotal),
            new HistoryPage<RunHistoryItem>(runs, skip, take, runTotal),
            new HistoryPage<AlarmHistoryItem>(alarms, skip, take, alarmTotal),
            new HistoryPage<ProtocolHistoryItem>(protocol, skip, take, protocolTotal));
    }

    private static JobHistoryItem Map(JobRow row) =>
        new(row.Id, row.Name, row.State, ToUtc(row.CreatedAtUtc));

    private static RunHistoryItem Map(RunRow row) =>
        new(
            row.Id,
            row.JobId,
            row.JobName,
            row.State,
            row.StartedAtUtc is null ? null : ToUtc(row.StartedAtUtc.Value),
            row.FinishedAtUtc is null ? null : ToUtc(row.FinishedAtUtc.Value),
            row.FailureReason);

    private static AlarmHistoryItem Map(AlarmRow row) =>
        new(
            row.Id,
            row.Code,
            row.Severity,
            row.Message,
            ToUtc(row.RaisedAtUtc),
            row.IsAcknowledged);

    private static ProtocolHistoryItem Map(ProtocolRow row) =>
        new(
            row.Id,
            row.SessionId,
            row.Sequence,
            row.Direction,
            row.MessageType,
            row.Payload,
            ToUtc(row.ObservedAtUtc));

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record JobRow(Guid Id, string Name, string State, DateTime CreatedAtUtc);

    private sealed record RunRow(
        Guid Id,
        Guid JobId,
        string JobName,
        string State,
        DateTime? StartedAtUtc,
        DateTime? FinishedAtUtc,
        string? FailureReason);

    private sealed record AlarmRow(
        Guid Id,
        string Code,
        string Severity,
        string Message,
        DateTime RaisedAtUtc,
        bool IsAcknowledged);

    private sealed record ProtocolRow(
        long Id,
        Guid SessionId,
        long Sequence,
        string Direction,
        string MessageType,
        string Payload,
        DateTime ObservedAtUtc);
}
