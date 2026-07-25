using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Application.Jobs;

public sealed record CreateJobCommand(string Name, string GCode);

public sealed record CreateJobResult(
    MachiningJob? Job,
    IReadOnlyList<GCodeDiagnostic> Diagnostics)
{
    public bool Succeeded => Job is not null;
}

public sealed class CreateJobHandler
{
    private readonly GCodeParser parser;
    private readonly IJobRepository repository;
    private readonly TimeProvider timeProvider;

    public CreateJobHandler(
        GCodeParser parser,
        IJobRepository repository,
        TimeProvider timeProvider)
    {
        this.parser = parser;
        this.repository = repository;
        this.timeProvider = timeProvider;
    }

    public async Task<CreateJobResult> HandleAsync(
        CreateJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<GCodeDiagnostic> diagnostics = [];
        string name = command.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            diagnostics.Add(new GCodeDiagnostic(
                0,
                DiagnosticSeverity.Error,
                "JOB001",
                "A job name is required."));
        }
        else if (name.Length > 120)
        {
            diagnostics.Add(new GCodeDiagnostic(
                0,
                DiagnosticSeverity.Error,
                "JOB002",
                "Job names cannot exceed 120 characters."));
        }

        ParsedGCodeProgram program = parser.Parse(
            name.Length == 0 ? "unnamed-job" : name,
            command.GCode ?? string.Empty);
        diagnostics.AddRange(program.Diagnostics);

        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new CreateJobResult(null, diagnostics);
        }

        MachiningJob job = MachiningJob.Create(
            Guid.NewGuid(),
            name,
            program,
            timeProvider.GetUtcNow());
        await repository.AddAsync(job, cancellationToken);
        return new CreateJobResult(job, diagnostics);
    }
}
