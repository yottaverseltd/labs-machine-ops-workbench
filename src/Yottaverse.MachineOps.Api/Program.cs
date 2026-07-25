using System.Text.Json.Serialization;
using Npgsql;
using Yottaverse.MachineOps.Api.Persistence;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Jobs;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Infrastructure.Database;

WebApplicationBuilder builder = WebApplication.CreateBuilder(
    new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GCodeParser>();

bool useVolatileStorage = builder.Environment.IsEnvironment("Testing");
if (useVolatileStorage)
{
    builder.Services.AddSingleton<IJobRepository, InMemoryJobRepository>();
}
else
{
    string connectionString = builder.Configuration.GetConnectionString("MachineOps")
        ?? throw new InvalidOperationException("ConnectionStrings:MachineOps is required.");
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<DatabaseMigrator>();
    builder.Services.AddScoped<IJobRepository, DapperJobRepository>();
}

builder.Services.AddScoped<CreateJobHandler>();
builder.Services.AddScoped<GetJobHandler>();
builder.Services.AddScoped<ListJobsHandler>();

WebApplication app = builder.Build();

if (!useVolatileStorage)
{
    await app.Services.GetRequiredService<DatabaseMigrator>()
        .MigrateAsync(CancellationToken.None);
}

app.UseExceptionHandler();
app.MapOpenApi();
app.MapGet(
        "/health",
        (TimeProvider timeProvider) => TypedResults.Ok(
            new ApiStatusDto("MachineOps API", "0.3.0", timeProvider.GetUtcNow())))
    .WithName("GetApiStatus")
    .WithTags("Operations");
if (!useVolatileStorage)
{
    app.MapGet(
            "/health/ready",
            async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
            {
                await using NpgsqlConnection connection =
                    await dataSource.OpenConnectionAsync(cancellationToken);
                await using NpgsqlCommand command = new("SELECT 1;", connection);
                await command.ExecuteScalarAsync(cancellationToken);
                return TypedResults.Ok(new { database = "ready" });
            })
        .WithName("GetReadiness")
        .WithTags("Operations");
}
app.MapControllers();

app.Run();

public partial class Program;
