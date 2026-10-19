using System.Runtime.InteropServices;

namespace ProjectCleaner.Interop;

/// <summary>
/// Window chrome bits. The title bar is drawn by DWM, not WPF, so it has to be told separately
/// whether the app is running dark - otherwise a dark Fluent surface keeps a stark white caption.
/// </summary>
internal static class DwmInterop
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Best effort only: an unsupported attribute or a missing dwmapi.dll just leaves the default
    /// caption in place, which is cosmetic and never a reason to fail startup.
    /// </summary>
    internal static void ApplyDarkCaption(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;

        try
        {
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}