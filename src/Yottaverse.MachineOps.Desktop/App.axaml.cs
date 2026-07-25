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
            HttpClient httpClient = new()
            {
                BaseAddress = ResolveApiAddress(),
                Timeout = TimeSpan.FromSeconds(5),
            };
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    new GCodeFilePicker(),
                    new Core.GCode.GCodeParser(),
                    new MachineOpsApiClient(httpClient)),
            };
            desktop.Exit += (_, _) => httpClient.Dispose();
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
