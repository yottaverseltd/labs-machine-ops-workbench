using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Api.Mappings;
using Yottaverse.MachineOps.Application.Alarms;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Core.Alarms;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/alarms")]
public sealed class AlarmsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlarmDto>>> List(
        [FromServices] AlarmService service,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MachineAlarm> alarms =
            await service.ListAsync(Math.Max(0, skip), take, cancellationToken);
        return Ok(alarms.Select(AlarmMappings.ToDto).ToArray());
    }

    [HttpPost("{id:guid}/acknowledgements")]
    [ProducesResponseType<AlarmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AlarmDto>> Acknowledge(
        Guid id,
        AcknowledgeAlarmRequest request,
        [FromServices] AlarmService service,
        CancellationToken cancellationToken)
    {
        try
        {
            MachineAlarm alarm = await service.AcknowledgeAsync(
                id,
                request.IdempotencyKey,
                request.AcknowledgedBy,
                request.Note,
                request.ExpectedVersion,
                cancellationToken);
            return Ok(alarm.ToDto());
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(Problem(
                title: "The alarm was not found.",
                detail: exception.Message,
                statusCode: StatusCodes.Status404NotFound));
        }
        catch (Exception exception) when (
            exception is AlarmConcurrencyException or InvalidOperationException)
        {
            return Conflict(Problem(
                title: "The alarm changed before it could be acknowledged.",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict));
        }
    }
}
