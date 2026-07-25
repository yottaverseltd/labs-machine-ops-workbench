using System.Text.Json.Serialization;
using Yottaverse.MachineOps.Api.Persistence;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Jobs;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;

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
builder.Services.AddSingleton<IJobRepository, InMemoryJobRepository>();
builder.Services.AddScoped<CreateJobHandler>();
builder.Services.AddScoped<GetJobHandler>();
builder.Services.AddScoped<ListJobsHandler>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapGet(
        "/health",
        (TimeProvider timeProvider) => TypedResults.Ok(
            new ApiStatusDto("MachineOps API", "0.2.0", timeProvider.GetUtcNow())))
    .WithName("GetApiStatus")
    .WithTags("Operations");
app.MapControllers();

app.Run();

public partial class Program;
