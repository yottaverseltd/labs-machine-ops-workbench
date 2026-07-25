using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.Jobs;
using Yottaverse.MachineOps.Core.Machines;
using Yottaverse.MachineOps.Core.Runs;

namespace Yottaverse.MachineOps.Application.Runs;

public sealed class RunCoordinator : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IControllerSession controllerSession;
    private readonly IJobRepository jobRepository;
    private readonly IRunRepository runRepository;
    private readonly TimeProvider timeProvider;
    private JobRun? activeRun;

    public RunCoordinator(
        IControllerSession controllerSession,
        IJobRepository jobRepository,
        IRunRepository runRepository,
        TimeProvider timeProvider)
    {
        this.controllerSession = controllerSession;
        this.jobRepository = jobRepository;
        this.runRepository = runRepository;
        this.timeProvider = timeProvider;
    }

    public JobRun? ActiveRun => activeRun;

    public async Task<JobRun> StartAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (activeRun?.State is JobRunState.Running or JobRunState.Paused)
            {
                throw new InvalidOperationException("A run is already active.");
            }

            MachiningJob? job = await jobRepository.GetAsync(jobId, cancellationToken);
            if (job is null)
            {
                throw new KeyNotFoundException($"Job '{jobId}' was not found.");
            }

            if (controllerSession.Snapshot.ConnectionStatus != ConnectionStatus.Connected)
            {
                throw new InvalidOperationException("Connect the simulator before starting a run.");
            }

            JobRun run = JobRun.Create(
                Guid.NewGuid(),
                job.Id,
                MachineIdentifiers.LocalSimulator);
            run.Start(timeProvider.GetUtcNow());
            await controllerSession.ExecuteAsync(ControllerOperation.Start, cancellationToken);
            await runRepository.SaveAsync(run, cancellationToken);
            activeRun = run;
            return run;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<JobRun> PauseAsync(CancellationToken cancellationToken) =>
        ChangeStateAsync(ControllerOperation.Pause, cancellationToken);

    public Task<JobRun> ResumeAsync(CancellationToken cancellationToken) =>
        ChangeStateAsync(ControllerOperation.Resume, cancellationToken);

    public Task<JobRun> CancelAsync(CancellationToken cancellationToken) =>
        ChangeStateAsync(ControllerOperation.Cancel, cancellationToken);

    public void Dispose() => gate.Dispose();

    public async Task<JobRun?> RefreshAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (activeRun is null)
            {
                return null;
            }

            MachineSnapshot snapshot = await controllerSession.RefreshAsync(cancellationToken);
            activeRun.ObserveProgress(
                snapshot.Progress,
                snapshot.LastAcknowledgedCommand,
                snapshot.ObservedAtUtc);
            await runRepository.SaveAsync(activeRun, cancellationToken);
            return activeRun;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<JobRun> ChangeStateAsync(
        ControllerOperation operation,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            JobRun run = activeRun ??
                throw new InvalidOperationException("There is no active run.");

            switch (operation)
            {
                case ControllerOperation.Pause:
                    run.Pause();
                    break;
                case ControllerOperation.Resume:
                    run.Resume();
                    break;
                case ControllerOperation.Cancel:
                    run.Cancel(timeProvider.GetUtcNow());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }

            try
            {
                await controllerSession.ExecuteAsync(operation, cancellationToken);
                await runRepository.SaveAsync(run, cancellationToken);
                return run;
            }
            catch
            {
                run.Fail($"Controller did not accept {operation}.", timeProvider.GetUtcNow());
                await runRepository.SaveAsync(run, CancellationToken.None);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
