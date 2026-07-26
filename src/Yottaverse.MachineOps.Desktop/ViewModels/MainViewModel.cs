using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Contracts.History;
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
    private Guid? currentAlarmId;
    private int currentAlarmVersion;

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
        liveClient.AlarmReceived += OnLiveAlarmReceived;
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
    public partial string MachineFeed { get; private set; } = "Not reported";

    [ObservableProperty]
    public partial string MachineSpindle { get; private set; } = "Not reported";

    [ObservableProperty]
    public partial Position3D? LiveToolPosition { get; private set; }

    [ObservableProperty]
    public partial bool HasLiveToolPosition { get; private set; }

    [ObservableProperty]
    public partial string ToolpathMode { get; private set; } = "PREVIEW";

    [ObservableProperty]
    public partial string ControllerError { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RunState { get; private set; } = "No active run";

    [ObservableProperty]
    public partial double RunProgress { get; private set; }

    [ObservableProperty]
    public partial string AlarmStatus { get; private set; } = "No open alarm";

    [ObservableProperty]
    public partial bool HasOpenAlarm { get; private set; }

    [ObservableProperty]
    public partial string HistorySearch { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ActivityLine> Activity { get; private set; } = [];

    [ObservableProperty]
    public partial string HistorySummary { get; private set; } = "Select Refresh to load persisted activity";

    private bool CanChangeProgram() => !IsRunActive();

    [RelayCommand(CanExecute = nameof(CanChangeProgram))]
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

    [RelayCommand(CanExecute = nameof(CanChangeProgram))]
    private void LoadSample()
    {
        LoadProgram("simple-pocket.ngc", DemoPrograms.SimplePocket);
    }

    private bool CanSave() =>
        currentProgram?.IsValid == true &&
        !IsBusy &&
        CanChangeProgram();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveJobAsync(CancellationToken cancellationToken)
    {
        if (currentProgram is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SaveJobCommand.NotifyCanExecuteChanged();
            ApiStatusDto apiStatus = await apiClient.GetStatusAsync(cancellationToken);
            ApiStatus = $"{apiStatus.Service} {apiStatus.Version}";
            JobDto job = await apiClient.CreateJobAsync(
                new CreateJobRequest(currentProgram.Name, currentProgram.Source),
                cancellationToken);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "Save cancelled.";
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

    private bool CanConnectSimulator() => !HasLiveToolPosition;

    [RelayCommand(CanExecute = nameof(CanConnectSimulator))]
    private async Task ConnectSimulatorAsync(CancellationToken cancellationToken)
    {
        try
        {
            ControllerError = string.Empty;
            MachineStatus = "Connecting";
            await liveClient.StartAsync(cancellationToken);
            MachineSnapshotDto snapshot = await apiClient.ConnectSimulatorAsync(
                5099,
                cancellationToken);
            ApplySnapshot(snapshot);
            Status = "Simulator connected through the API.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MachineStatus = "Disconnected";
            Status = "Connection cancelled.";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            MachineStatus = "Connection failed";
            ControllerError = exception.Message;
            Status = "The simulator could not be reached on port 5099.";
        }
    }

    private bool CanDisconnectSimulator() => HasLiveToolPosition;

    [RelayCommand(CanExecute = nameof(CanDisconnectSimulator))]
    private async Task DisconnectSimulatorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await apiClient.DisconnectSimulatorAsync(cancellationToken);
            ApplySnapshot(await apiClient.GetMachineSnapshotAsync(
                false,
                cancellationToken));
            Status = "Simulator disconnected.";
        }
        catch (HttpRequestException exception)
        {
            ControllerError = exception.Message;
            Status = "The API could not disconnect the simulator cleanly.";
        }
    }

    private bool CanStartRun() =>
        savedJobId.HasValue &&
        HasLiveToolPosition &&
        !IsRunActive();

    [RelayCommand(CanExecute = nameof(CanStartRun))]
    private async Task StartRunAsync(CancellationToken cancellationToken)
    {
        if (savedJobId is not Guid jobId)
        {
            return;
        }

        try
        {
            JobRunDto run = await apiClient.StartRunAsync(jobId, cancellationToken);
            ApplyRun(run);
            Status = "Run started. Live machine state is updating.";
            while (run.State is "Running" or "Paused")
            {
                await Task.Delay(250, cancellationToken);
                run = await apiClient.RefreshRunAsync(cancellationToken);
                ApplyRun(run);
            }

            Status = $"Run finished with state {run.State}.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Status = "Run monitoring cancelled. The simulator state is unchanged.";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            ControllerError = exception.Message;
            Status = "The run command failed.";
        }
    }

    private bool CanPauseRun() => RunState == "Running";

    [RelayCommand(CanExecute = nameof(CanPauseRun))]
    private async Task PauseRunAsync(CancellationToken cancellationToken) =>
        await SendRunCommandAsync("pause", cancellationToken);

    private bool CanResumeRun() => RunState == "Paused";

    [RelayCommand(CanExecute = nameof(CanResumeRun))]
    private async Task ResumeRunAsync(CancellationToken cancellationToken) =>
        await SendRunCommandAsync("resume", cancellationToken);

    private bool CanCancelRun() => IsRunActive();

    [RelayCommand(CanExecute = nameof(CanCancelRun))]
    private async Task CancelRunAsync(CancellationToken cancellationToken) =>
        await SendRunCommandAsync("cancel", cancellationToken);

    private async Task SendRunCommandAsync(
        string command,
        CancellationToken cancellationToken)
    {
        try
        {
            JobRunDto run = await apiClient.SendRunCommandAsync(command, cancellationToken);
            ApplyRun(run);
            Status = run.State switch
            {
                "Paused" => "Run paused.",
                "Running" => "Run resumed.",
                "Cancelled" => "Run cancelled.",
                _ => $"Run state changed to {run.State}.",
            };
        }
        catch (HttpRequestException exception)
        {
            ControllerError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task AcknowledgeAlarmAsync(CancellationToken cancellationToken)
    {
        if (currentAlarmId is not Guid alarmId)
        {
            return;
        }

        try
        {
            AlarmDto alarm = await apiClient.AcknowledgeAlarmAsync(
                alarmId,
                new AcknowledgeAlarmRequest(
                    Guid.NewGuid(),
                    Environment.UserName,
                    "Acknowledged from the workbench.",
                    currentAlarmVersion),
                cancellationToken);
            ApplyAlarm(alarm.Id, alarm.Code, alarm.Message, alarm.Version, alarm.IsAcknowledged);
        }
        catch (HttpRequestException exception)
        {
            ControllerError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SearchHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            OperationsHistoryDto history = await apiClient.SearchHistoryAsync(
                HistorySearch,
                0,
                50,
                cancellationToken);
            Activity = BuildActivity(history);
            HistorySummary =
                $"{history.Jobs.Total} jobs, {history.Runs.Total} runs, " +
                $"{history.Alarms.Total} alarms, {history.ProtocolMessages.Total} protocol messages";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            HistorySummary = "History refresh cancelled";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            HistorySummary = "History is unavailable while the API is offline";
            ControllerError = exception.Message;
        }
    }

    private void ApplySnapshot(MachineSnapshotDto snapshot)
    {
        MachineStatus = $"{snapshot.ConnectionStatus} / {snapshot.OperatingStatus}";
        MachinePosition =
            $"X {snapshot.X:0.000}  Y {snapshot.Y:0.000}  Z {snapshot.Z:0.000}";
        MachineFeed = snapshot.FeedRate is double feedRate
            ? $"{feedRate.ToString("N0", CultureInfo.CurrentCulture)} mm/min"
            : "Not reported";
        MachineSpindle = snapshot.SpindleSpeed is double spindleSpeed
            ? $"{spindleSpeed.ToString("N0", CultureInfo.CurrentCulture)} rpm"
            : "Not reported";
        bool isConnected = string.Equals(
            snapshot.ConnectionStatus,
            "Connected",
            StringComparison.OrdinalIgnoreCase);
        HasLiveToolPosition = isConnected;
        LiveToolPosition = isConnected
            ? new Position3D(snapshot.X, snapshot.Y, snapshot.Z)
            : null;
        ToolpathMode = isConnected ? "LIVE" : "PREVIEW";
        ControllerError = snapshot.LastError ?? string.Empty;
        if (IsRunActive())
        {
            RunProgress = snapshot.Progress;
        }

        ConnectSimulatorCommand.NotifyCanExecuteChanged();
        DisconnectSimulatorCommand.NotifyCanExecuteChanged();
        StartRunCommand.NotifyCanExecuteChanged();
    }

    private void ApplyRun(JobRunDto run)
    {
        RunState = run.State;
        if (run.State == "Completed")
        {
            RunProgress = 100;
        }

        NotifyRunCommandStates();
    }

    private bool IsRunActive() => RunState is "Running" or "Paused";

    private void NotifyRunCommandStates()
    {
        OpenFileCommand.NotifyCanExecuteChanged();
        LoadSampleCommand.NotifyCanExecuteChanged();
        SaveJobCommand.NotifyCanExecuteChanged();
        StartRunCommand.NotifyCanExecuteChanged();
        PauseRunCommand.NotifyCanExecuteChanged();
        ResumeRunCommand.NotifyCanExecuteChanged();
        CancelRunCommand.NotifyCanExecuteChanged();
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

    private void OnLiveAlarmReceived(object? sender, LiveAlarmEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(
            () => ApplyAlarm(
                eventArgs.Alarm.Id,
                eventArgs.Alarm.Code,
                eventArgs.Alarm.Message,
                eventArgs.Alarm.Version,
                eventArgs.Alarm.IsAcknowledged));

    private void ApplyAlarm(
        Guid id,
        string code,
        string message,
        int version,
        bool acknowledged)
    {
        currentAlarmId = acknowledged ? null : id;
        currentAlarmVersion = version;
        HasOpenAlarm = !acknowledged;
        AlarmStatus = acknowledged ? $"{code} acknowledged" : $"{code}: {message}";
    }

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
        RunState = "No active run";
        RunProgress = 0;
        NotifyRunCommandStates();
    }

    private static ActivityLine[] BuildActivity(OperationsHistoryDto history)
    {
        IEnumerable<ActivityLine> jobs = history.Jobs.Items.Select(item =>
            new ActivityLine(
                item.CreatedAtUtc,
                "JOB",
                item.Name,
                item.State));
        IEnumerable<ActivityLine> runs = history.Runs.Items.Select(item =>
            new ActivityLine(
                item.StartedAtUtc ?? DateTimeOffset.MinValue,
                "RUN",
                item.JobName,
                item.FailureReason is null ? item.State : $"{item.State}: {item.FailureReason}"));
        IEnumerable<ActivityLine> alarms = history.Alarms.Items.Select(item =>
            new ActivityLine(
                item.RaisedAtUtc,
                "ALARM",
                $"{item.Code} / {item.Severity}",
                item.IsAcknowledged ? $"{item.Message} (acknowledged)" : item.Message));
        IEnumerable<ActivityLine> protocol = history.ProtocolMessages.Items.Select(item =>
            new ActivityLine(
                item.ObservedAtUtc,
                item.Direction.ToUpperInvariant(),
                $"#{item.Sequence} {item.MessageType}",
                item.Payload));

        return jobs
            .Concat(runs)
            .Concat(alarms)
            .Concat(protocol)
            .OrderByDescending(line => line.ObservedAtUtc)
            .ToArray();
    }

    private sealed class DesignFilePicker : IGCodeFilePicker
    {
        public Task<PickedGCodeFile?> PickAsync() => Task.FromResult<PickedGCodeFile?>(null);
    }

    private sealed class DesignApiClient : IMachineOpsApiClient
    {
        public Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ApiStatusDto("MachineOps API", "1.0.0", DateTimeOffset.UtcNow));

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

        public Task<IReadOnlyList<AlarmDto>> ListAlarmsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AlarmDto>>([]);

        public Task<AlarmDto> AcknowledgeAlarmAsync(
            Guid alarmId,
            AcknowledgeAlarmRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OperationsHistoryDto> SearchHistoryAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new OperationsHistoryDto(
                    new PageDto<JobHistoryDto>([], skip, take, 0),
                    new PageDto<RunHistoryDto>([], skip, take, 0),
                    new PageDto<AlarmHistoryDto>([], skip, take, 0),
                    new PageDto<ProtocolMessageDto>([], skip, take, 0)));
    }

    private sealed class DesignLiveClient : IMachineLiveClient
    {
        public event EventHandler<LiveSnapshotEventArgs>? SnapshotReceived;

        public event EventHandler<LiveConnectionStateEventArgs>? ConnectionStateChanged;

        public event EventHandler<LiveAlarmEventArgs>? AlarmReceived;

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
            AlarmReceived?.Invoke(
                this,
                new LiveAlarmEventArgs(
                    new AlarmNotificationDto(
                        Guid.NewGuid(),
                        Guid.Parse("e0df4a6f-5578-4d53-85b0-17f3828b087d"),
                        "E_STOP",
                        "Emergency stop input is active.",
                        0,
                        false,
                        DateTimeOffset.UtcNow)));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            SnapshotReceived = null;
            ConnectionStateChanged = null;
            AlarmReceived = null;
            return ValueTask.CompletedTask;
        }
    }
}

public sealed record ActivityLine(
    DateTimeOffset ObservedAtUtc,
    string Kind,
    string Summary,
    string Detail)
{
    public string Time => ObservedAtUtc == DateTimeOffset.MinValue
        ? "pending"
        : ObservedAtUtc.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
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
