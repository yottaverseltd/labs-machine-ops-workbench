using System.Text.Json.Serialization;
using Npgsql;
using Yottaverse.MachineOps.Api.Live;
using Yottaverse.MachineOps.Api.Persistence;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Alarms;
using Yottaverse.MachineOps.Application.Jobs;
using Yottaverse.MachineOps.Application.Machines;
using Yottaverse.MachineOps.Application.Runs;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Infrastructure.Controller;
using Yottaverse.MachineOps.Infrastructure.Database;
using Yottaverse.MachineOps.Infrastructure.Diagnostics;

if (args is ["--health-check"])
{
    try
    {
        using HttpClient healthClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        using HttpResponseMessage response =
            await healthClient.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

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
builder.Services.AddSignalR();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(
    new ControllerConnectionDefaults(
        builder.Configuration["Simulator:Host"] ?? "127.0.0.1",
        TimeSpan.FromSeconds(5)));
builder.Services.AddSingleton<GCodeParser>();

bool useVolatileStorage = builder.Environment.IsEnvironment("Testing");
if (useVolatileStorage)
{
    builder.Services.AddSingleton<IJobRepository, InMemoryJobRepository>();
    builder.Services.AddSingleton<IRunRepository, InMemoryRunRepository>();
    builder.Services.AddSingleton<IControllerAuditStore, InMemoryControllerAuditStore>();
    builder.Services.AddSingleton<IAlarmRepository, InMemoryAlarmRepository>();
    builder.Services.AddSingleton<IOutboxStore, InMemoryOutboxStore>();
    builder.Services.AddSingleton<IDiagnosticExporter, InMemoryDiagnosticExporter>();
}
else
{
    string connectionString = builder.Configuration.GetConnectionString("MachineOps")
        ?? throw new InvalidOperationException("ConnectionStrings:MachineOps is required.");
    builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
    builder.Services.AddSingleton<DatabaseMigrator>();
    builder.Services.AddSingleton<IJobRepository, DapperJobRepository>();
    builder.Services.AddSingleton<IRunRepository, DapperRunRepository>();
    builder.Services.AddSingleton<IControllerAuditStore, DapperControllerAuditStore>();
    builder.Services.AddSingleton<IAlarmRepository, DapperAlarmRepository>();
    builder.Services.AddSingleton<IOutboxStore, DapperOutboxStore>();
    builder.Services.AddSingleton<IDiagnosticExporter, ZipDiagnosticExporter>();
}

builder.Services.AddScoped<CreateJobHandler>();
builder.Services.AddScoped<GetJobHandler>();
builder.Services.AddScoped<ListJobsHandler>();
builder.Services.AddSingleton<TcpControllerSession>();
builder.Services.AddSingleton<IControllerSession>(
    services => services.GetRequiredService<TcpControllerSession>());
builder.Services.AddScoped<ConnectSimulatorHandler>();
builder.Services.AddScoped<GetMachineSnapshotHandler>();
builder.Services.AddScoped<DisconnectMachineHandler>();
builder.Services.AddSingleton<RunCoordinator>();
builder.Services.AddSingleton<AlarmService>();
builder.Services.AddHostedService<MachineUpdateBroadcaster>();
builder.Services.AddHostedService<RunMonitorService>();
builder.Services.AddHostedService<AlarmIngestService>();
builder.Services.AddHostedService<OutboxPublisherService>();

WebApplication app = builder.Build();

if (!useVolatileStorage)
{
    await app.Services.GetRequiredService<DatabaseMigrator>()
        .MigrateAsync(CancellationToken.None);
}

app.UseExceptionHandler();
app.Use(
    async (context, next) =>
    {
        string correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        using IDisposable? scope = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MachineOps.Request")
            .BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        await next(context);
    });
app.MapOpenApi();
app.MapGet(
        "/health",
        (TimeProvider timeProvider) => TypedResults.Ok(
            new ApiStatusDto("MachineOps API", "0.7.0", timeProvider.GetUtcNow())))
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
app.MapHub<MachineHub>("/hubs/machines");

app.Run();

public partial class Program;
