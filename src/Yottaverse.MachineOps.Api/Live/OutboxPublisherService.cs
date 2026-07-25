using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Contracts.Live;

namespace Yottaverse.MachineOps.Api.Live;

public sealed class OutboxPublisherService : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> LogPublishFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1002, "OutboxPublishFailed"),
            "Outbox message {MessageId} could not be published.");

    private readonly IHubContext<MachineHub> hubContext;
    private readonly ILogger<OutboxPublisherService> logger;
    private readonly IOutboxStore outboxStore;
    private readonly TimeProvider timeProvider;

    public OutboxPublisherService(
        IHubContext<MachineHub> hubContext,
        ILogger<OutboxPublisherService> logger,
        IOutboxStore outboxStore,
        TimeProvider timeProvider)
    {
        this.hubContext = hubContext;
        this.logger = logger;
        this.outboxStore = outboxStore;
        this.timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(300));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            IReadOnlyList<ClaimedOutboxMessage> messages = await outboxStore.ClaimAsync(
                20,
                timeProvider.GetUtcNow(),
                stoppingToken);
            foreach (ClaimedOutboxMessage message in messages)
            {
                await PublishAsync(message, stoppingToken);
            }
        }
    }

    private async Task PublishAsync(
        ClaimedOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            AlarmNotificationDto notification =
                JsonSerializer.Deserialize<AlarmNotificationDto>(message.Payload)
                ?? throw new InvalidDataException("The alarm outbox payload was empty.");
            string eventName = message.MessageType switch
            {
                "alarm.raised" => MachineLiveEventNames.AlarmRaised,
                "alarm.acknowledged" => MachineLiveEventNames.AlarmAcknowledged,
                _ => throw new InvalidDataException(
                    $"Outbox type '{message.MessageType}' is not supported."),
            };
            await hubContext.Clients.All.SendAsync(
                eventName,
                notification,
                cancellationToken);
            await outboxStore.MarkProcessedAsync(
                message.Id,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or IOException)
        {
            LogPublishFailure(logger, message.Id, exception);
            await outboxStore.MarkFailedAsync(
                message.Id,
                exception.Message,
                cancellationToken);
        }
    }
}
