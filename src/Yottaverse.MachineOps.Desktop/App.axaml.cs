using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(new GCodeFilePicker(), new Core.GCode.GCodeParser()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
