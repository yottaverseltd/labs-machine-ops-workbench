using System.IO.Compression;
using System.Text;
using Yottaverse.MachineOps.Application.Abstractions;

namespace Yottaverse.MachineOps.Api.Persistence;

public sealed class InMemoryDiagnosticExporter : IDiagnosticExporter
{
    public Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("manifest.json");
            using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
            writer.Write("""{"product":"MachineOps Workbench","storage":"in-memory"}""");
        }

        return Task.FromResult(output.ToArray());
    }
}
