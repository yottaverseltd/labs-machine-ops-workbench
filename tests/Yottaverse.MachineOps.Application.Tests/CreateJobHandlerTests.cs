using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Application.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;

namespace Yottaverse.MachineOps.Application.Tests;

public sealed class CreateJobHandlerTests
{
    [Fact]
    public async Task ValidProgramCreatesDraftJob()
    {
        RecordingJobRepository repository = new();
        DateTimeOffset now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        CreateJobHandler handler = new(new GCodeParser(), repository, new StubTimeProvider(now));

        CreateJobResult result = await handler.HandleAsync(
            new CreateJobCommand("Bracket", "G21 G90\nG0 X0 Y0\nG1 X20 Y10 F400"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Job);
        Assert.Equal(JobState.Draft, result.Job.State);
        Assert.Equal(now, result.Job.CreatedAtUtc);
        Assert.Same(result.Job, repository.SavedJob);
    }

    [Fact]
    public async Task InvalidProgramIsNotPersisted()
    {
        RecordingJobRepository repository = new();
        CreateJobHandler handler = new(
            new GCodeParser(),
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        CreateJobResult result = await handler.HandleAsync(
            new CreateJobCommand("Broken", "G1 X20 F0"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(repository.SavedJob);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "GCODE003");
    }

    [Fact]
    public async Task MissingNameReturnsApplicationValidationError()
    {
        RecordingJobRepository repository = new();
        CreateJobHandler handler = new(
            new GCodeParser(),
            repository,
            new StubTimeProvider(DateTimeOffset.UtcNow));

        CreateJobResult result = await handler.HandleAsync(
            new CreateJobCommand(" ", "G1 X20 F100"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "JOB001");
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public StubTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingJobRepository : IJobRepository
    {
        public MachiningJob? SavedJob { get; private set; }

        public Task AddAsync(MachiningJob job, CancellationToken cancellationToken)
        {
            SavedJob = job;
            return Task.CompletedTask;
        }

        public Task<MachiningJob?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(SavedJob?.Id == id ? SavedJob : null);

        public Task<IReadOnlyList<MachiningJob>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MachiningJob>>(
                SavedJob is null ? [] : [SavedJob]);
    }
}
