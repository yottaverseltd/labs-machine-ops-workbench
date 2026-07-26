using Xunit;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Contracts.History;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Contracts.Runs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Desktop.Services;
using Yottaverse.MachineOps.Desktop.ViewModels;

namespace Yottaverse.MachineOps.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void StartsWithAUsableLocalSample()
    {
        MainViewModel viewModel = CreateViewModel(new StubApiClient());

        Assert.Equal("simple-pocket.ngc", viewModel.FileName);
        Assert.NotEmpty(viewModel.Segments);
        Assert.Contains("Ready to inspect", viewModel.Status, StringComparison.Ordinal);
        Assert.True(viewModel.SaveJobCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveUsesTheApiBoundaryAndKeepsLocalStateReadable()
    {
        StubApiClient api = new();
        MainViewModel viewModel = CreateViewModel(api);

        await viewModel.SaveJobCommand.ExecuteAsync(null);

        Assert.NotNull(api.LastCreateJobRequest);
        Assert.Equal("simple-pocket.ngc", api.LastCreateJobRequest.Name);
        Assert.Equal("4B6467EA", viewModel.SavedJobReference);
        Assert.Equal("MachineOps API 1.0.0", viewModel.ApiStatus);
        Assert.False(viewModel.StartRunCommand.CanExecute(null));
    }

    [Fact]
    public async Task SearchCombinesPersistedRecordsInTimeOrder()
    {
        DateTimeOffset now = DateTimeOffset.Parse(
            "2026-07-25T12:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        StubApiClient api = new()
        {
            History = new OperationsHistoryDto(
                new PageDto<JobHistoryDto>(
                    [new JobHistoryDto(Guid.NewGuid(), "Bracket", "Draft", now.AddMinutes(-4))],
                    0,
                    50,
                    1),
                new PageDto<RunHistoryDto>(
                    [
                        new RunHistoryDto(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Bracket",
                            "Completed",
                            now.AddMinutes(-3),
                            now.AddMinutes(-2),
                            null),
                    ],
                    0,
                    50,
                    1),
                new PageDto<AlarmHistoryDto>(
                    [
                        new AlarmHistoryDto(
                            Guid.NewGuid(),
                            "DOOR",
                            "Warning",
                            "Door opened",
                            now.AddMinutes(-1),
                            true),
                    ],
                    0,
                    50,
                    1),
                new PageDto<ProtocolMessageDto>(
                    [
                        new ProtocolMessageDto(
                            42,
                            Guid.NewGuid(),
                            7,
                            "Inbound",
                            "state",
                            "{\"state\":\"Idle\"}",
                            now),
                    ],
                    0,
                    50,
                    1)),
        };
        MainViewModel viewModel = CreateViewModel(api);

        await viewModel.SearchHistoryCommand.ExecuteAsync(null);

        Assert.Equal(4, viewModel.Activity.Count);
        Assert.Equal("INBOUND", viewModel.Activity[0].Kind);
        Assert.Equal("JOB", viewModel.Activity[^1].Kind);
        Assert.Equal("1 jobs, 1 runs, 1 alarms, 1 protocol messages", viewModel.HistorySummary);
    }

    [Fact]
    public async Task ConnectDisplaysLivePositionFeedAndSpindle()
    {
        StubApiClient api = new()
        {
            ConnectedSnapshot = new MachineSnapshotDto(
                Guid.NewGuid(),
                "Connected",
                "Running",
                12.5,
                7.25,
                -2,
                600,
                12_000,
                35,
                4,
                9,
                null,
                DateTimeOffset.UtcNow),
        };
        MainViewModel viewModel = CreateViewModel(api);

        await viewModel.ConnectSimulatorCommand.ExecuteAsync(null);

        Assert.Equal("Connected / Running", viewModel.MachineStatus);
        Assert.Equal("X 12.500  Y 7.250  Z -2.000", viewModel.MachinePosition);
        Assert.Contains("600", viewModel.MachineFeed, StringComparison.Ordinal);
        Assert.Contains("12", viewModel.MachineSpindle, StringComparison.Ordinal);
        Assert.Equal(new Position3D(12.5, 7.25, -2), viewModel.LiveToolPosition);
        Assert.True(viewModel.HasLiveToolPosition);
        Assert.Equal("LIVE", viewModel.ToolpathMode);
        Assert.Equal(0, viewModel.RunProgress);
        Assert.False(viewModel.ConnectSimulatorCommand.CanExecute(null));
        Assert.True(viewModel.DisconnectSimulatorCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavedConnectedJobCanStart()
    {
        StubApiClient api = new()
        {
            ConnectedSnapshot = ConnectedIdleSnapshot(),
        };
        MainViewModel viewModel = CreateViewModel(api);

        await viewModel.SaveJobCommand.ExecuteAsync(null);
        await viewModel.ConnectSimulatorCommand.ExecuteAsync(null);

        Assert.True(viewModel.StartRunCommand.CanExecute(null));
    }

    private static MainViewModel CreateViewModel(StubApiClient api) =>
        new(new NullFilePicker(), new GCodeParser(), api, new StubLiveClient());

    private static MachineSnapshotDto ConnectedIdleSnapshot() =>
        new(
            Guid.NewGuid(),
            "Connected",
            "Idle",
            0,
            0,
            0,
            null,
            null,
            0,
            0,
            1,
            null,
            DateTimeOffset.UtcNow);

    private sealed class NullFilePicker : IGCodeFilePicker
    {
        public Task<PickedGCodeFile?> PickAsync() => Task.FromResult<PickedGCodeFile?>(null);
    }

    private sealed class StubLiveClient : IMachineLiveClient
    {
        public event EventHandler<LiveSnapshotEventArgs>? SnapshotReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<LiveConnectionStateEventArgs>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<LiveAlarmEventArgs>? AlarmReceived
        {
            add { }
            remove { }
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubApiClient : IMachineOpsApiClient
    {
        private static readonly Guid SavedJobId =
            Guid.Parse("4b6467ea-7754-4a14-9e44-b732572f9bd1");

        public CreateJobRequest? LastCreateJobRequest { get; private set; }

        public OperationsHistoryDto History { get; init; } = new(
            new PageDto<JobHistoryDto>([], 0, 50, 0),
            new PageDto<RunHistoryDto>([], 0, 50, 0),
            new PageDto<AlarmHistoryDto>([], 0, 50, 0),
            new PageDto<ProtocolMessageDto>([], 0, 50, 0));

        public MachineSnapshotDto? ConnectedSnapshot { get; init; }

        public Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ApiStatusDto("MachineOps API", "1.0.0", DateTimeOffset.UtcNow));

        public Task<JobDto> CreateJobAsync(
            CreateJobRequest request,
            CancellationToken cancellationToken)
        {
            LastCreateJobRequest = request;
            return Task.FromResult(
                new JobDto(
                    SavedJobId,
                    request.Name,
                    "Draft",
                    DateTimeOffset.UtcNow,
                    request.GCode,
                    1,
                    1,
                    1,
                    1,
                    [],
                    []));
        }

        public Task<OperationsHistoryDto> SearchHistoryAsync(
            string? query,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(History);

        public Task<MachineSnapshotDto> ConnectSimulatorAsync(
            int port,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                ConnectedSnapshot ??
                throw new NotSupportedException());

        public Task<MachineSnapshotDto> GetMachineSnapshotAsync(
            bool refresh,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DisconnectSimulatorAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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
    }
}
