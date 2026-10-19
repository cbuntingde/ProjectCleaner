using System.Windows;
using System.Windows.Threading;
using ProjectCleaner.Services;

namespace ProjectCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Follow the Windows app-mode setting before the first window is realised.
        AppTheme.Initialise();

        // A COM or XAML failure inside a background delete would otherwise tear the process down
        // with no explanation; surface it and let the window keep running.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}",
            "Project Cleaner",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}