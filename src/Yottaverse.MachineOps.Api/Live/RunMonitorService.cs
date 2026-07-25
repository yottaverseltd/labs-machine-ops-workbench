using Yottaverse.MachineOps.Application.Runs;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Api.Live;

public sealed class RunMonitorService : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Action<ILogger, Exception?> LogSamplingFailure =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, "SimulatorSamplingFailed"),
            "The active simulator run could not be sampled.");
    private readonly ILogger<RunMonitorService> logger;
    private readonly RunCoordinator runCoordinator;

    public RunMonitorService(
        RunCoordinator runCoordinator,
        ILogger<RunMonitorService> logger)
    {
        this.runCoordinator = runCoordinator;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SampleInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (runCoordinator.ActiveRun?.State != JobRunState.Running)
            {
                continue;
            }

            try
            {
                await runCoordinator.RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (
                exception is IOException or TimeoutException or InvalidOperationException)
            {
                LogSamplingFailure(logger, exception);
            }
        }
    }
}
