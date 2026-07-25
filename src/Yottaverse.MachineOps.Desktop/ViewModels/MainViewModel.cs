using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Contracts.Runs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Desktop.Services;

namespace Yottaverse.MachineOps.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IGCodeFilePicker filePicker;
    private readonly GCodeParser parser;
    private readonly IMachineOpsApiClient apiClient;
    private readonly IMachineLiveClient liveClient;
    private readonly object liveSnapshotGate = new();
    private ParsedGCodeProgram? currentProgram;
    private Guid? savedJobId;
    private MachineSnapshotDto? pendingLiveSnapshot;
    private int liveDispatchScheduled;

    public MainViewModel()
        : this(
            new DesignFilePicker(),
            new GCodeParser(),
            new DesignApiClient(),
            new DesignLiveClient())
    {
    }

    public MainViewModel(
        IGCodeFilePicker filePicker,
        GCodeParser parser,
        IMachineOpsApiClient apiClient,
        IMachineLiveClient liveClient)
    {
        this.filePicker = filePicker;
        this.parser = parser;
        this.apiClient = apiClient;
        this.liveClient = liveClient;
        liveClient.SnapshotReceived += OnLiveSnapshotReceived;
        liveClient.ConnectionStateChanged += OnLiveConnectionStateChanged;
        LoadProgram("simple-pocket.ngc", DemoPrograms.SimplePocket);
    }

    [ObservableProperty]
    public partial string FileName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Source { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ToolpathSegment> Segments { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<GCodeDiagnostic> Diagnostics { get; private set; } = [];

    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int SegmentCount { get; private set; }

    [ObservableProperty]
    public partial int WarningCount { get; private set; }

    [ObservableProperty]
    public partial string TravelDistance { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkArea { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiStatus { get; private set; } = "API not checked";

    [ObservableProperty]
    public partial string LiveStatus { get; private set; } = "Live feed offline";

    [ObservableProperty]
    public partial string SavedJobReference { get; private set; } = "Not saved";

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string MachineStatus { get; private set; } = "Disconnected";

    [ObservableProperty]
    public partial string MachinePosition { get; private set; } = "X 0.000  Y 0.000  Z 0.000";

    [ObservableProperty]
    public partial string ControllerError { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RunState { get; private set; } = "No active run";

    [ObservableProperty]
    public partial double RunProgress { get; private set; }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        try
        {
            PickedGCodeFile? file = await filePicker.PickAsync();
            if (file is not null)
            {
                LoadProgram(file.Name, file.Content);
            }
        }
        catch (IOException exception)
        {
            Status = $"Could not read the selected file: {exception.Message}";
        }
    }

    [RelayCommand]
    private void LoadSample()
    {
        LoadProgram("simple-pocket.ngc", DemoPrograms.SimplePocket);
    }

    private bool CanSave() => currentProgram?.IsValid == true && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveJobAsync()
    {
        if (currentProgram is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SaveJobCommand.NotifyCanExecuteChanged();
            ApiStatusDto apiStatus = await apiClient.GetStatusAsync(CancellationToken.None);
            ApiStatus = $"{apiStatus.Service} {apiStatus.Version}";
            JobDto job = await apiClient.CreateJobAsync(
                new CreateJobRequest(currentProgram.Name, currentProgram.Source),
                CancellationToken.None);
            SavedJobReference = job.Id.ToString("N")[..8].ToUpperInvariant();
            savedJobId = job.Id;
            Status = $"Saved {job.Name} through the API.";
            StartRunCommand.NotifyCanExecuteChanged();
        }
        catch (HttpRequestException)
        {
            ApiStatus = "API offline";
            Status = "The API is unavailable. Local preview remains ready.";
        }
        catch (TaskCanceledException)
        {
            ApiStatus = "API timeout";
            Status = "The API did not respond within five seconds.";
        }
        finally
        {
            IsBusy = false;
            SaveJobCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task ConnectSimulatorAsync()
    {
        try
        {
            ControllerError = string.Empty;
            MachineStatus = "Connecting";
            await liveClient.StartAsync(CancellationToken.None);
            MachineSnapshotDto snapshot = await apiClient.ConnectSimulatorAsync(
                5099,
                CancellationToken.None);
            ApplySnapshot(snapshot);
            Status = "Simulator connected through the API.";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            MachineStatus = "Connection failed";
            ControllerError = exception.Message;
            Status = "The simulator could not be reached on port 5099.";
        }
    }

    [RelayCommand]
    private async Task DisconnectSimulatorAsync()
    {
        try
        {
            await apiClient.DisconnectSimulatorAsync(CancellationToken.None);
            ApplySnapshot(await apiClient.GetMachineSnapshotAsync(
                false,
                CancellationToken.None));
            Status = "Simulator disconnected.";
        }
        catch (HttpRequestException exception)
        {
            ControllerError = exception.Message;
            Status = "The API could not disconnect the simulator cleanly.";
        }
    }

    private bool CanStartRun() => savedJobId.HasValue;

    [RelayCommand(CanExecute = nameof(CanStartRun))]
    private async Task StartRunAsync()
    {
        if (savedJobId is not Guid jobId)
        {
            return;
        }

        try
        {
            JobRunDto run = await apiClient.StartRunAsync(jobId, CancellationToken.None);
            ApplyRun(run);
            while (run.State is "Running" or "Paused")
            {
                await Task.Delay(250);
                run = await apiClient.RefreshRunAsync(CancellationToken.None);
                ApplyRun(run);
            }

            Status = $"Run finished with state {run.State}.";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            ControllerError = exception.Message;
            Status = "The run command failed.";
        }
    }

    [RelayCommand]
    private async Task PauseRunAsync() => await SendRunCommandAsync("pause");

    [RelayCommand]
    private async Task ResumeRunAsync() => await SendRunCommandAsync("resume");

    [RelayCommand]
    private async Task CancelRunAsync() => await SendRunCommandAsync("cancel");

    private async Task SendRunCommandAsync(string command)
    {
        try
        {
            ApplyRun(await apiClient.SendRunCommandAsync(command, CancellationToken.None));
        }
        catch (HttpRequestException exception)
        {
            ControllerError = exception.Message;
        }
    }

    private void ApplySnapshot(MachineSnapshotDto snapshot)
    {
        MachineStatus = $"{snapshot.ConnectionStatus} / {snapshot.OperatingStatus}";
        MachinePosition =
            $"X {snapshot.X:0.000}  Y {snapshot.Y:0.000}  Z {snapshot.Z:0.000}";
        ControllerError = snapshot.LastError ?? string.Empty;
        RunProgress = snapshot.Progress;
    }

    private void ApplyRun(JobRunDto run)
    {
        RunState = run.State;
        if (run.State == "Completed")
        {
            RunProgress = 100;
        }
    }

    private void OnLiveSnapshotReceived(object? sender, LiveSnapshotEventArgs eventArgs)
    {
        lock (liveSnapshotGate)
        {
            pendingLiveSnapshot = eventArgs.Snapshot;
        }

        if (Interlocked.Exchange(ref liveDispatchScheduled, 1) == 0)
        {
            Dispatcher.UIThread.Post(ApplyLatestLiveSnapshot, DispatcherPriority.Background);
        }
    }

    private void ApplyLatestLiveSnapshot()
    {
        MachineSnapshotDto? latest;
        lock (liveSnapshotGate)
        {
            latest = pendingLiveSnapshot;
            pendingLiveSnapshot = null;
        }

        if (latest is not null)
        {
            ApplySnapshot(latest);
        }

        Interlocked.Exchange(ref liveDispatchScheduled, 0);
        lock (liveSnapshotGate)
        {
            if (pendingLiveSnapshot is not null &&
                Interlocked.Exchange(ref liveDispatchScheduled, 1) == 0)
            {
                Dispatcher.UIThread.Post(ApplyLatestLiveSnapshot, DispatcherPriority.Background);
            }
        }
    }

    private void OnLiveConnectionStateChanged(
        object? sender,
        LiveConnectionStateEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(() => LiveStatus = $"Live feed {eventArgs.State.ToLowerInvariant()}");

    private void LoadProgram(string name, string source)
    {
        ParsedGCodeProgram program = parser.Parse(name, source);
        currentProgram = program;
        FileName = program.Name;
        Source = program.Source;
        Segments = program.Segments;
        Diagnostics = program.Diagnostics;
        SegmentCount = program.Segments.Count;
        WarningCount = program.Diagnostics.Count(diagnostic =>
            diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        TravelDistance = $"{program.TravelDistance:0.0} mm";
        WorkArea = $"{program.Bounds.Width:0.0} x {program.Bounds.Height:0.0} mm";
        Status = program.IsValid
            ? $"Ready to inspect. {program.Segments.Count} moves parsed."
            : "The program has validation errors.";
        SavedJobReference = "Not saved";
        savedJobId = null;
        StartRunCommand.NotifyCanExecuteChanged();
        SaveJobCommand.NotifyCanExecuteChanged();
    }

    private sealed class DesignFilePicker : IGCodeFilePicker
    {
        public Task<PickedGCodeFile?> PickAsync() => Task.FromResult<PickedGCodeFile?>(null);
    }

    private sealed class DesignApiClient : IMachineOpsApiClient
    {
        public Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ApiStatusDto("MachineOps API", "0.2.0", DateTimeOffset.UtcNow));

        public Task<JobDto> CreateJobAsync(
            CreateJobRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MachineSnapshotDto> ConnectSimulatorAsync(
            int port,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MachineSnapshotDto(
                Guid.Parse("e0df4a6f-5578-4d53-85b0-17f3828b087d"),
                "Connected",
                "Idle",
                0,
                0,
                5,
                null,
                null,
                0,
                0,
                1,
                null,
                DateTimeOffset.UtcNow));

        public Task<MachineSnapshotDto> GetMachineSnapshotAsync(
            bool refresh,
            CancellationToken cancellationToken) =>
            ConnectSimulatorAsync(5099, cancellationToken);

        public Task DisconnectSimulatorAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<JobRunDto> StartRunAsync(
            Guid jobId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobRunDto> RefreshRunAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobRunDto> SendRunCommandAsync(
            string command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class DesignLiveClient : IMachineLiveClient
    {
        public event EventHandler<LiveSnapshotEventArgs>? SnapshotReceived;

        public event EventHandler<LiveConnectionStateEventArgs>? ConnectionStateChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ConnectionStateChanged?.Invoke(
                this,
                new LiveConnectionStateEventArgs("Live"));
            SnapshotReceived?.Invoke(
                this,
                new LiveSnapshotEventArgs(
                    new MachineSnapshotDto(
                        Guid.Parse("e0df4a6f-5578-4d53-85b0-17f3828b087d"),
                        "Connected",
                        "Idle",
                        0,
                        0,
                        5,
                        null,
                        null,
                        0,
                        0,
                        1,
                        null,
                        DateTimeOffset.UtcNow)));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            SnapshotReceived = null;
            ConnectionStateChanged = null;
            return ValueTask.CompletedTask;
        }
    }
}

internal static class DemoPrograms
{
    public const string SimplePocket =
        """
        %
        (Simple rectangular pocket preview)
        G21 G90
        G0 X0 Y0 Z5
        G0 X10 Y10
        G1 Z-2 F180
        G1 X70 Y10 F600
        G1 X70 Y50
        G1 X10 Y50
        G1 X10 Y10
        G1 X18 Y18
        G1 X62 Y18
        G1 X62 Y42
        G1 X18 Y42
        G1 X18 Y18
        G0 Z5
        G0 X0 Y0
        M30
        %
        """;
}
