using System.Windows;

namespace ProjectCleaner.Services;

/// <summary>
/// Opens the Windows folder picker. <see cref="System.Windows.Forms.FolderBrowserDialog"/> is used
/// solely for its Vista IFileDialog implementation, which is the modern dialog on Windows 10/11.
/// </summary>
public sealed class FolderPicker
{
    public string? PickFolder(IntPtr ownerHandle, string? startAt)
    {
        // Fully qualified: the WinForms implicit usings are switched off project-wide.
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose the folder to scan",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            AutoUpgradeEnabled = true,
        };

        if (!string.IsNullOrWhiteSpace(startAt) && Directory.Exists(startAt))
        {
            dialog.SelectedPath = startAt;
        }

        var owner = ownerHandle == IntPtr.Zero ? null : new Win32Owner(ownerHandle);

        return dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    /// <summary>Lets the WinForms dialog parent itself to our WPF window.</summary>
    private sealed class Win32Owner : System.Windows.Forms.IWin32Window
    {
        public Win32Owner(IntPtr handle) => Handle = handle;

        public IntPtr Handle { get; }
    }
}