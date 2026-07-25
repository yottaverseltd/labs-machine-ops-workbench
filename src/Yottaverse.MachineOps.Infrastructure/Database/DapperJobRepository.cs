using System.Text.Json;
using Dapper;
using Npgsql;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DapperJobRepository : IJobRepository
{
    private const string SelectColumns =
        """
        id AS Id,
        name AS Name,
        gcode AS GCode,
        state AS State,
        created_at_utc AS CreatedAtUtc,
        minimum_x AS MinimumX,
        minimum_y AS MinimumY,
        maximum_x AS MaximumX,
        maximum_y AS MaximumY,
        toolpath::text AS ToolpathJson,
        diagnostics::text AS DiagnosticsJson
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private readonly NpgsqlDataSource dataSource;

    public DapperJobRepository(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task AddAsync(MachiningJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        const string sql =
            """
            INSERT INTO jobs
            (
                id,
                name,
                gcode,
                state,
                created_at_utc,
                segment_count,
                travel_distance,
                minimum_x,
                minimum_y,
                maximum_x,
                maximum_y,
                toolpath,
                diagnostics
            )
            VALUES
            (
                @Id,
                @Name,
                @GCode,
                @State,
                @CreatedAtUtc,
                @SegmentCount,
                @TravelDistance,
                @MinimumX,
                @MinimumY,
                @MaximumX,
                @MaximumY,
                CAST(@ToolpathJson AS jsonb),
                CAST(@DiagnosticsJson AS jsonb)
            );
            """;

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                job.Id,
                job.Name,
                GCode = job.Program.Source,
                State = job.State.ToString(),
                job.CreatedAtUtc,
                SegmentCount = job.Program.Segments.Count,
                job.Program.TravelDistance,
                job.Program.Bounds.MinimumX,
                job.Program.Bounds.MinimumY,
                job.Program.Bounds.MaximumX,
                job.Program.Bounds.MaximumY,
                ToolpathJson = JsonSerializer.Serialize(job.Program.Segments, JsonOptions),
                DiagnosticsJson = JsonSerializer.Serialize(job.Program.Diagnostics, JsonOptions),
            },
            cancellationToken: cancellationToken));
    }

    public async Task<MachiningJob?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        string sql = $"SELECT {SelectColumns} FROM jobs WHERE id = @Id;";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        JobRow? row = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<MachiningJob>> ListAsync(CancellationToken cancellationToken)
    {
        string sql = $"SELECT {SelectColumns} FROM jobs ORDER BY created_at_utc DESC;";
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<JobRow> rows = await connection.QueryAsync<JobRow>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    private static MachiningJob Map(JobRow row)
    {
        ToolpathSegment[] segments = JsonSerializer.Deserialize<ToolpathSegment[]>(
            row.ToolpathJson,
            JsonOptions) ?? [];
        GCodeDiagnostic[] diagnostics = JsonSerializer.Deserialize<GCodeDiagnostic[]>(
            row.DiagnosticsJson,
            JsonOptions) ?? [];
        ParsedGCodeProgram program = new(
            row.Name,
            row.GCode,
            segments,
            diagnostics,
            new ToolpathBounds(row.MinimumX, row.MinimumY, row.MaximumX, row.MaximumY));

        return MachiningJob.Rehydrate(
            row.Id,
            row.Name,
            program,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc)),
            Enum.Parse<JobState>(row.State));
    }

    private sealed record JobRow(
        Guid Id,
        string Name,
        string GCode,
        string State,
        DateTime CreatedAtUtc,
        double MinimumX,
        double MinimumY,
        double MaximumX,
        double MaximumY,
        string ToolpathJson,
        string DiagnosticsJson);
}
