using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Desktop.Services;

namespace Yottaverse.MachineOps.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IGCodeFilePicker filePicker;
    private readonly GCodeParser parser;
    private readonly IMachineOpsApiClient apiClient;
    private ParsedGCodeProgram? currentProgram;

    public MainViewModel()
        : this(new DesignFilePicker(), new GCodeParser(), new DesignApiClient())
    {
    }

    public MainViewModel(
        IGCodeFilePicker filePicker,
        GCodeParser parser,
        IMachineOpsApiClient apiClient)
    {
        this.filePicker = filePicker;
        this.parser = parser;
        this.apiClient = apiClient;
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
    public partial string SavedJobReference { get; private set; } = "Not saved";

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

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
            Status = $"Saved {job.Name} through the API.";
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
