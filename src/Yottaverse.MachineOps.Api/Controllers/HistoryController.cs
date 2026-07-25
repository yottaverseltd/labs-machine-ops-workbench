using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Contracts.History;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<OperationsHistoryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationsHistoryDto>> Search(
        [FromQuery, StringLength(200)] string? query,
        [FromQuery, Range(0, int.MaxValue)] int skip,
        [FromQuery, Range(1, 100)] int? take,
        [FromServices] IOperationsHistoryQuery historyQuery,
        CancellationToken cancellationToken)
    {
        OperationsHistory history = await historyQuery.SearchAsync(
            query,
            skip,
            take ?? 25,
            cancellationToken);
        return Ok(ToDto(history));
    }

    private static OperationsHistoryDto ToDto(OperationsHistory history) =>
        new(
            new PageDto<JobHistoryDto>(
                history.Jobs.Items.Select(item => new JobHistoryDto(
                    item.Id,
                    item.Name,
                    item.State,
                    item.CreatedAtUtc)).ToArray(),
                history.Jobs.Skip,
                history.Jobs.Take,
                history.Jobs.Total),
            new PageDto<RunHistoryDto>(
                history.Runs.Items.Select(item => new RunHistoryDto(
                    item.Id,
                    item.JobId,
                    item.JobName,
                    item.State,
                    item.StartedAtUtc,
                    item.FinishedAtUtc,
                    item.FailureReason)).ToArray(),
                history.Runs.Skip,
                history.Runs.Take,
                history.Runs.Total),
            new PageDto<AlarmHistoryDto>(
                history.Alarms.Items.Select(item => new AlarmHistoryDto(
                    item.Id,
                    item.Code,
                    item.Severity,
                    item.Message,
                    item.RaisedAtUtc,
                    item.IsAcknowledged)).ToArray(),
                history.Alarms.Skip,
                history.Alarms.Take,
                history.Alarms.Total),
            new PageDto<ProtocolMessageDto>(
                history.ProtocolMessages.Items.Select(item => new ProtocolMessageDto(
                    item.Id,
                    item.SessionId,
                    item.Sequence,
                    item.Direction,
                    item.MessageType,
                    item.Payload,
                    item.ObservedAtUtc)).ToArray(),
                history.ProtocolMessages.Skip,
                history.ProtocolMessages.Take,
                history.ProtocolMessages.Total));
}
