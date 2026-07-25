using System.Threading.Channels;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Alarms;
using Yottaverse.MachineOps.Application.Runs;

namespace Yottaverse.MachineOps.Api.Live;

public sealed class AlarmIngestService : BackgroundService
{
    private readonly AlarmService alarmService;
    private readonly IControllerSession controllerSession;
    private readonly RunCoordinator runCoordinator;
    private readonly Channel<ControllerAlarmEventArgs> alarms =
        Channel.CreateBounded<ControllerAlarmEventArgs>(
            new BoundedChannelOptions(32)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

    public AlarmIngestService(
        AlarmService alarmService,
        IControllerSession controllerSession,
        RunCoordinator runCoordinator)
    {
        this.alarmService = alarmService;
        this.controllerSession = controllerSession;
        this.runCoordinator = runCoordinator;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        controllerSession.AlarmRaised += OnAlarmRaised;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        controllerSession.AlarmRaised -= OnAlarmRaised;
        alarms.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (ControllerAlarmEventArgs alarm in alarms.Reader.ReadAllAsync(stoppingToken))
        {
            await alarmService.RaiseAsync(
                alarm.ExternalKey,
                alarm.Code,
                alarm.Message,
                runCoordinator.ActiveRun?.Id,
                stoppingToken);
        }
    }

    private void OnAlarmRaised(object? sender, ControllerAlarmEventArgs eventArgs)
    {
        if (!alarms.Writer.TryWrite(eventArgs))
        {
            throw new InvalidOperationException("The bounded alarm queue is full.");
        }
    }
}
