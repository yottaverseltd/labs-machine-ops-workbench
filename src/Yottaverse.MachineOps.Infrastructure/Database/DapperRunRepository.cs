using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperRunRepository : IRunRepository
{
    private readonly NpgsqlDataSource dataSource;

    public DapperRunRepository(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task SaveAsync(JobRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        const string ensureMachineSql =
            """
            INSERT INTO machines (id, name, host, port, scenario, created_at_utc)
            VALUES (@MachineId, 'Local simulator', '127.0.0.1', 5099, 'Normal', @CreatedAtUtc)
            ON CONFLICT (id) DO NOTHING;
            """;
        const string saveRunSql =
            """
            INSERT INTO job_runs
            (
                id,
                job_id,
                machine_id,
                state,
                started_at_utc,
                finished_at_utc,
                last_command_index,
                failure_reason
            )
            VALUES
            (
                @Id,
                @JobId,
                @MachineId,
                @State,
                @StartedAtUtc,
                @FinishedAtUtc,
                @LastCommandIndex,
                @FailureReason
            )
            ON CONFLICT (id) DO UPDATE SET
                state = EXCLUDED.state,
                finished_at_utc = EXCLUDED.finished_at_utc,
                last_command_index = EXCLUDED.last_command_index,
                failure_reason = EXCLUDED.failure_reason;
            """;

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            ensureMachineSql,
            new
            {
                run.MachineId,
                CreatedAtUtc = run.StartedAtUtc ?? DateTimeOffset.UtcNow,
            },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            saveRunSql,
            new
            {
                run.Id,
                run.JobId,
                run.MachineId,
                State = run.State.ToString(),
                run.StartedAtUtc,
                run.FinishedAtUtc,
                run.LastCommandIndex,
                run.FailureReason,
            },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }
}
