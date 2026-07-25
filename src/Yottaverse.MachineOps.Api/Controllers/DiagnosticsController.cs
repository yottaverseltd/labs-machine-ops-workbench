using Microsoft.AspNetCore.Mvc;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    [HttpGet("export")]
    [Produces("application/zip")]
    public async Task<IActionResult> Export(
        [FromServices] IDiagnosticExporter exporter,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        byte[] bundle = await exporter.ExportAsync(cancellationToken);
        return File(
            bundle,
            "application/zip",
            $"machineops-diagnostics-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.zip");
    }
}
