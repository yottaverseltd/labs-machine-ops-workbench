using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Yottaverse.MachineOps.Desktop.Services;
using Yottaverse.MachineOps.Desktop.ViewModels;
using Yottaverse.MachineOps.Desktop.Views;

namespace Yottaverse.MachineOps.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Uri apiAddress = ResolveApiAddress();
            HttpClient httpClient = new()
            {
                BaseAddress = apiAddress,
                Timeout = TimeSpan.FromSeconds(5),
            };
            MachineOpsApiClient apiClient = new(httpClient);
            MachineLiveClient liveClient = new(apiAddress, apiClient);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    new GCodeFilePicker(),
                    new Core.GCode.GCodeParser(),
                    apiClient,
                    liveClient),
            };
            desktop.Exit += async (_, _) =>
            {
                await liveClient.DisposeAsync();
                httpClient.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Uri ResolveApiAddress()
    {
        string? configuredAddress = Environment.GetEnvironmentVariable("MACHINEOPS_API_URL");
        return Uri.TryCreate(configuredAddress, UriKind.Absolute, out Uri? address)
            ? address
            : new Uri("http://localhost:5080", UriKind.Absolute);
    }
}
