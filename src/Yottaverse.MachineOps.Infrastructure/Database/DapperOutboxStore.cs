using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperOutboxStore : IOutboxStore
{
    private readonly NpgsqlDataSource dataSource;

    public DapperOutboxStore(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(
        int batchSize,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            WITH candidates AS
            (
                SELECT id
                FROM outbox_messages
                WHERE processed_at_utc IS NULL
                  AND (locked_until_utc IS NULL OR locked_until_utc < @NowUtc)
                ORDER BY occurred_at_utc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE outbox_messages message
            SET locked_until_utc = @LockedUntilUtc,
                attempt_count = attempt_count + 1
            FROM candidates
            WHERE message.id = candidates.id
            RETURNING
                message.id AS Id,
                message.message_type AS MessageType,
                message.payload::text AS Payload;
            """;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<ClaimedOutboxMessage> messages =
            await connection.QueryAsync<ClaimedOutboxMessage>(new CommandDefinition(
                sql,
                new
                {
                    BatchSize = batchSize,
                    NowUtc = nowUtc,
                    LockedUntilUtc = nowUtc.AddSeconds(30),
                },
                cancellationToken: cancellationToken));
        return messages.ToArray();
    }

    public async Task MarkProcessedAsync(
        Guid id,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE outbox_messages
            SET processed_at_utc = @ProcessedAtUtc,
                locked_until_utc = NULL,
                last_error = NULL
            WHERE id = @Id;
            """;
        await ExecuteAsync(sql, new { Id = id, ProcessedAtUtc = processedAtUtc }, cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid id,
        string failure,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE outbox_messages
            SET locked_until_utc = NULL,
                last_error = @Error
            WHERE id = @Id;
            """;
        await ExecuteAsync(sql, new { Id = id, Error = failure }, cancellationToken);
    }

    private async Task ExecuteAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken));
    }
}
