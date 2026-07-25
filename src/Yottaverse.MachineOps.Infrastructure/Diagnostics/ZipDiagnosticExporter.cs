using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Infrastructure.Diagnostics;

public sealed class ZipDiagnosticExporter : IDiagnosticExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider timeProvider;

    public ZipDiagnosticExporter(NpgsqlDataSource dataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.timeProvider = timeProvider;
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<dynamic> jobs = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT id, name, state, created_at_utc, segment_count, travel_distance
            FROM jobs ORDER BY created_at_utc DESC LIMIT 500;
            """,
            cancellationToken: cancellationToken));
        IEnumerable<dynamic> runs = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT id, job_id, machine_id, state, started_at_utc, finished_at_utc,
                   last_command_index, failure_reason
            FROM job_runs ORDER BY started_at_utc DESC NULLS LAST LIMIT 500;
            """,
            cancellationToken: cancellationToken));
        IEnumerable<dynamic> alarms = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT id, machine_id, job_run_id, code, severity, message,
                   raised_at_utc, version
            FROM alarms ORDER BY raised_at_utc DESC LIMIT 500;
            """,
            cancellationToken: cancellationToken));
        IEnumerable<dynamic> protocol = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT session_id, sequence, direction, message_type, payload, observed_at_utc
            FROM protocol_messages ORDER BY observed_at_utc DESC LIMIT 2000;
            """,
            cancellationToken: cancellationToken));

        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteJsonAsync(
                archive,
                "manifest.json",
                new
                {
                    Product = "MachineOps Workbench",
                    ExportedAtUtc = timeProvider.GetUtcNow(),
                    FormatVersion = 1,
                },
                cancellationToken);
            await WriteJsonAsync(archive, "jobs.json", jobs, cancellationToken);
            await WriteJsonAsync(archive, "runs.json", runs, cancellationToken);
            await WriteJsonAsync(archive, "alarms.json", alarms, cancellationToken);
            await WriteJsonAsync(archive, "protocol.json", protocol, cancellationToken);
        }

        return output.ToArray();
    }

    private static async Task WriteJsonAsync(
        ZipArchive archive,
        string name,
        object value,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
