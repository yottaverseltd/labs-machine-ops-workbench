using Npgsql;
using Testcontainers.PostgreSql;
using Yottaverse.MachineOps.Infrastructure.Database;

namespace Yottaverse.MachineOps.Infrastructure.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder("postgres:18.4-alpine3.24")
            .WithDatabase("machineops_test")
            .WithUsername("machineops")
            .WithPassword("machineops")
            .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        DataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        DatabaseMigrator migrator = new(DataSource, TimeProvider.System);
        await migrator.MigrateAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }

        await container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresTestGroup : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
