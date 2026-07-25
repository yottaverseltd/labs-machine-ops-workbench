using System.Data;
using System.Reflection;
using Dapper;
using Npgsql;

namespace Yottaverse.MachineOps.Infrastructure.Database;

public sealed class DatabaseMigrator
{
    private const string CreateVersionTableSql =
        """
        CREATE TABLE IF NOT EXISTS schema_versions
        (
            version varchar(100) PRIMARY KEY,
            applied_at_utc timestamptz NOT NULL
        );
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider timeProvider;

    public DatabaseMigrator(NpgsqlDataSource dataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.timeProvider = timeProvider;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            CreateVersionTableSql,
            cancellationToken: cancellationToken));

        HashSet<string> appliedVersions = (await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT version FROM schema_versions;",
                cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.Ordinal);

        foreach (Migration migration in await LoadMigrationsAsync(cancellationToken))
        {
            if (appliedVersions.Contains(migration.Version))
            {
                continue;
            }

            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    migration.Sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO schema_versions (version, applied_at_utc)
                    VALUES (@Version, @AppliedAtUtc);
                    """,
                    new
                    {
                        migration.Version,
                        AppliedAtUtc = timeProvider.GetUtcNow(),
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private static async Task<IReadOnlyList<Migration>> LoadMigrationsAsync(
        CancellationToken cancellationToken)
    {
        Assembly assembly = typeof(DatabaseMigrator).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Database.Migrations.", StringComparison.Ordinal) &&
                           name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        List<Migration> migrations = new(resources.Length);
        foreach (string resource in resources)
        {
            await using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Migration resource '{resource}' was not found.");
            using StreamReader reader = new(stream);
            string sql = await reader.ReadToEndAsync(cancellationToken);
            string version = resource[(resource.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..^4];
            migrations.Add(new Migration(version, sql));
        }

        return migrations;
    }

    private sealed record Migration(string Version, string Sql);
}
