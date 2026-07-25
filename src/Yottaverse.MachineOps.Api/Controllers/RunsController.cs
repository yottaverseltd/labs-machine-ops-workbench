using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Api.Mappings;
using Yottaverse.MachineOps.Application.Runs;
using Yottaverse.MachineOps.Contracts.Runs;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class RunsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<JobRunDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobRunDto>> Start(
        StartRunRequest request,
        [FromServices] RunCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        try
        {
            JobRun run = await coordinator.StartAsync(request.JobId, cancellationToken);
            return CreatedAtAction(nameof(GetActive), run.ToDto());
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(Problem(
                title: "The job was not found.",
                detail: exception.Message,
                statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(Problem(
                title: "The run could not be started.",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict));
        }
    }

    [HttpGet("active")]
    [ProducesResponseType<JobRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<JobRunDto> GetActive([FromServices] RunCoordinator coordinator) =>
        coordinator.ActiveRun is JobRun run ? Ok(run.ToDto()) : NotFound();

    [HttpPost("active/refresh")]
    public async Task<ActionResult<JobRunDto>> Refresh(
        [FromServices] RunCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        JobRun? run = await coordinator.RefreshAsync(cancellationToken);
        return run is null ? NotFound() : Ok(run.ToDto());
    }

    [HttpPost("active/pause")]
    public Task<ActionResult<JobRunDto>> Pause(
        [FromServices] RunCoordinator coordinator,
        CancellationToken cancellationToken) =>
        ApplyAsync(() => coordinator.PauseAsync(cancellationToken));

    [HttpPost("active/resume")]
    public Task<ActionResult<JobRunDto>> Resume(
        [FromServices] RunCoordinator coordinator,
        CancellationToken cancellationToken) =>
        ApplyAsync(() => coordinator.ResumeAsync(cancellationToken));

    [HttpPost("active/cancel")]
    public Task<ActionResult<JobRunDto>> Cancel(
        [FromServices] RunCoordinator coordinator,
        CancellationToken cancellationToken) =>
        ApplyAsync(() => coordinator.CancelAsync(cancellationToken));

    private async Task<ActionResult<JobRunDto>> ApplyAsync(Func<Task<JobRun>> operation)
    {
        try
        {
            return Ok((await operation()).ToDto());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(Problem(
                title: "The run command was rejected.",
                detail: exception.Message,
                statusCode: StatusCodes.Status409Conflict));
        }
    }
}
