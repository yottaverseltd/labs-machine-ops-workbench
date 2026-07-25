using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperControllerAuditStore : IControllerAuditStore
{
    private readonly NpgsqlDataSource dataSource;

    public DapperControllerAuditStore(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<Guid> BeginSessionAsync(
        Guid machineId,
        string host,
        int port,
        DateTimeOffset connectedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO machines (id, name, host, port, scenario, created_at_utc)
            VALUES (@MachineId, 'Local simulator', @Host, @Port, 'Selected at startup', @ConnectedAtUtc)
            ON CONFLICT (id) DO UPDATE SET
                host = EXCLUDED.host,
                port = EXCLUDED.port;

            INSERT INTO controller_sessions
                (id, machine_id, connected_at_utc, last_sequence)
            VALUES
                (@SessionId, @MachineId, @ConnectedAtUtc, 0);
            """;
        Guid sessionId = Guid.NewGuid();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SessionId = sessionId,
                MachineId = machineId,
                Host = host,
                Port = port,
                ConnectedAtUtc = connectedAtUtc,
            },
            cancellationToken: cancellationToken));
        return sessionId;
    }

    public async Task RecordAsync(
        Guid sessionId,
        long sequence,
        string direction,
        string messageType,
        string payload,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO protocol_messages
                (session_id, sequence, direction, message_type, payload, observed_at_utc)
            VALUES
                (@SessionId, @Sequence, @Direction, @MessageType, @Payload, @ObservedAtUtc);

            UPDATE controller_sessions
            SET last_sequence = GREATEST(last_sequence, @Sequence)
            WHERE id = @SessionId;
            """;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SessionId = sessionId,
                Sequence = sequence,
                Direction = direction,
                MessageType = messageType,
                Payload = payload,
                ObservedAtUtc = observedAtUtc,
            },
            cancellationToken: cancellationToken));
    }

    public async Task EndSessionAsync(
        Guid sessionId,
        string? reason,
        DateTimeOffset disconnectedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE controller_sessions
            SET disconnected_at_utc = @DisconnectedAtUtc,
                disconnect_reason = @Reason
            WHERE id = @SessionId
              AND disconnected_at_utc IS NULL;
            """;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SessionId = sessionId,
                DisconnectedAtUtc = disconnectedAtUtc,
                Reason = reason,
            },
            cancellationToken: cancellationToken));
    }
}
