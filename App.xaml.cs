using System.Windows;
using DeskLofi.Services;
using DeskLofi.Views;

namespace DeskLofi;

public partial class App : System.Windows.Application
{
    private MainWindow? _main;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = new SettingsService();
        _main = new MainWindow(settings);
        _main.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _main?.Dispose();
        base.OnExit(e);
    }
}
