using CuteSpace.Services;
using Microsoft.UI.Xaml;

namespace CuteSpace;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            SafeLog.Write("Fatal", e.ExceptionObject?.ToString() ?? "Unknown fatal error");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            SafeLog.Write("Task", e.Exception.ToString());
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        SafeLog.Write("Unhandled", e.Exception.ToString());
        e.Handled = true;
    }
}
