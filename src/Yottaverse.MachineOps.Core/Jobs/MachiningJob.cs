using Yottaverse.MachineOps.Core.GCode;

namespace Yottaverse.MachineOps.Core.Jobs;

public sealed class MachiningJob
{
    private MachiningJob(
        Guid id,
        string name,
        ParsedGCodeProgram program,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        Program = program;
        CreatedAtUtc = createdAtUtc;
        State = JobState.Draft;
    }

    public Guid Id { get; }

    public string Name { get; }

    public ParsedGCodeProgram Program { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public JobState State { get; private set; }

    public static MachiningJob Create(
        Guid id,
        string name,
        ParsedGCodeProgram program,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A job identifier is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(program);

        if (name.Length > 120)
        {
            throw new ArgumentException("Job names cannot exceed 120 characters.", nameof(name));
        }

        if (!program.IsValid)
        {
            throw new ArgumentException("A job requires a valid G-code program.", nameof(program));
        }

        return new MachiningJob(id, name.Trim(), program, createdAtUtc);
    }

    public static MachiningJob Rehydrate(
        Guid id,
        string name,
        ParsedGCodeProgram program,
        DateTimeOffset createdAtUtc,
        JobState state)
    {
        MachiningJob job = Create(id, name, program, createdAtUtc);
        job.State = state;
        return job;
    }
}
