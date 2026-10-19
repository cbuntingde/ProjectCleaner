using System.Windows;
using System.Windows.Interop;
using ProjectCleaner.Interop;
using ProjectCleaner.Services;
using ProjectCleaner.ViewModels;

namespace ProjectCleaner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var viewModel = new MainViewModel(
            new ShellDeleteService(),
            () => new WindowInteropHelper(this).Handle)
        {
            DialogHostReference = DialogHostRoot,
        };

        DataContext = viewModel;

        Activated += (_, _) => viewModel.RevalidateRoot();

        // DWM owns the caption, so it needs the same light/dark decision as the WPF surface.
        SourceInitialized += (_, _) =>
            DwmInterop.ApplyDarkCaption(new WindowInteropHelper(this).Handle, AppTheme.IsDark);
    }
}