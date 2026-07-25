using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Api.Mappings;
using Yottaverse.MachineOps.Application.Jobs;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<JobDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        CreateJobRequest request,
        [FromServices] CreateJobHandler handler,
        CancellationToken cancellationToken)
    {
        CreateJobResult result = await handler.HandleAsync(
            new CreateJobCommand(request.Name, request.GCode),
            cancellationToken);

        if (!result.Succeeded)
        {
            ProblemDetails problem = new()
            {
                Title = "The job could not be created.",
                Detail = "Review the G-code findings and submit the job again.",
                Status = StatusCodes.Status422UnprocessableEntity,
            };
            problem.Extensions["diagnostics"] = result.Diagnostics.Select(JobMappings.ToDto).ToArray();
            return UnprocessableEntity(problem);
        }

        JobDto response = result.Job!.ToDto();
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> Get(
        Guid id,
        [FromServices] GetJobHandler handler,
        CancellationToken cancellationToken)
    {
        MachiningJob? job = await handler.HandleAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job.ToDto());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<JobSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobSummaryDto>>> List(
        [FromServices] ListJobsHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MachiningJob> jobs = await handler.HandleAsync(cancellationToken);
        return Ok(jobs.Select(JobMappings.ToSummaryDto).ToArray());
    }
}
