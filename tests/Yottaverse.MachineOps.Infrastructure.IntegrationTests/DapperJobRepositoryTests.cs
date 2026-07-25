using Npgsql;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;
using Yottaverse.MachineOps.Infrastructure.Database;

namespace Yottaverse.MachineOps.Infrastructure.IntegrationTests;

[Collection(PostgresTestGroup.Name)]
public sealed class DapperJobRepositoryTests
{
    private readonly PostgresFixture fixture;

    public DapperJobRepositoryTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task InitialMigrationCreatesEveryOperationalTable()
    {
        string[] expectedTables =
        [
            "alarm_acknowledgements",
            "alarms",
            "controller_sessions",
            "job_commands",
            "job_runs",
            "jobs",
            "machine_samples",
            "machines",
            "outbox_messages",
            "protocol_messages",
            "schema_versions",
        ];

        await using NpgsqlConnection connection =
            await fixture.DataSource.OpenConnectionAsync(CancellationToken.None);
        await using NpgsqlCommand command = new(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            ORDER BY table_name;
            """,
            connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CancellationToken.None);
        List<string> actualTables = [];
        while (await reader.ReadAsync(CancellationToken.None))
        {
            actualTables.Add(reader.GetString(0));
        }

        Assert.Equal(expectedTables, actualTables);
    }

    [Fact]
    public async Task JobRoundTripsThroughPostgres()
    {
        GCodeParser parser = new();
        ParsedGCodeProgram program = parser.Parse(
            "Storage sample",
            "G21 G90\nG0 X2 Y3\nG1 X40 Y20 F500");
        MachiningJob expected = MachiningJob.Create(
            Guid.NewGuid(),
            "Storage sample",
            program,
            DateTimeOffset.UtcNow);
        DapperJobRepository repository = new(fixture.DataSource);

        await repository.AddAsync(expected, CancellationToken.None);
        MachiningJob? actual = await repository.GetAsync(expected.Id, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.Program.Segments, actual.Program.Segments);
        Assert.Equal(expected.Program.Diagnostics, actual.Program.Diagnostics);
    }

    [Fact]
    public async Task MigrationCanRunMoreThanOnce()
    {
        DatabaseMigrator migrator = new(fixture.DataSource, TimeProvider.System);

        await migrator.MigrateAsync(CancellationToken.None);
        await migrator.MigrateAsync(CancellationToken.None);

        await using NpgsqlConnection connection =
            await fixture.DataSource.OpenConnectionAsync(CancellationToken.None);
        await using NpgsqlCommand command = new(
            "SELECT COUNT(*) FROM schema_versions;",
            connection);
        object? count = await command.ExecuteScalarAsync(CancellationToken.None);
        Assert.Equal(2L, count);
    }
}
