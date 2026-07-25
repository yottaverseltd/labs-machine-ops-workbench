namespace Yottaverse.MachineOps.Application.Abstractions;

public interface IDiagnosticExporter
{
    public Task<byte[]> ExportAsync(CancellationToken cancellationToken);
}
