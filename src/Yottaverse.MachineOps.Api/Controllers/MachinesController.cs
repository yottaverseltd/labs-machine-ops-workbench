using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Api.Mappings;
using Yottaverse.MachineOps.Application.Machines;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/machines/simulator")]
public sealed class MachinesController : ControllerBase
{
    [HttpPost("connect")]
    [ProducesResponseType<MachineSnapshotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MachineSnapshotDto>> Connect(
        ConnectSimulatorRequest request,
        [FromServices] ConnectSimulatorHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            MachineSnapshot snapshot = await handler.HandleAsync(request.Port, cancellationToken);
            return Ok(snapshot.ToDto());
        }
        catch (Exception exception) when (
            exception is SocketException or IOException or TimeoutException or InvalidDataException)
        {
            return Problem(
                title: "The simulator could not be connected.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet]
    [ProducesResponseType<MachineSnapshotDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MachineSnapshotDto>> GetSnapshot(
        [FromQuery] bool refresh,
        [FromServices] GetMachineSnapshotHandler handler,
        CancellationToken cancellationToken)
    {
        MachineSnapshot snapshot = await handler.HandleAsync(refresh, cancellationToken);
        return Ok(snapshot.ToDto());
    }

    [HttpPost("disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(
        [FromServices] DisconnectMachineHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(cancellationToken);
        return NoContent();
    }
}
