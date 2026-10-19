using System.Runtime.InteropServices;

namespace ProjectCleaner.Interop;

/// <summary>
/// Minimal COM vtable declarations for the pieces of the Windows shell API needed to send items
/// to the Recycle Bin and to receive per-item success/failure from the operation.
///
/// Every method must be declared in exact vtable order - including the ones never called - because
/// interop dispatches positionally. Unused parameters are typed as <see cref="IntPtr"/> so no
/// marshaller ever has to guess.
/// </summary>
[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    void GetParent(out IShellItem ppsi);

    void GetDisplayName(uint sigdnName, out IntPtr ppszName);

    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[Guid("4BAAE148-53CD-4F75-9E1C-BC24D0B3DA3B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperationProgressSink
{
    void StartOperations();

    void FinishOperations(int hrResult);

    void PreRenameItem(uint dwFlags, IShellItem psiItem, IntPtr pszNewName);

    void PostRenameItem(uint dwFlags, IShellItem psiItem, IntPtr pszNewName, int hrRename, IntPtr psiNewlyCreated);

    void PreMoveItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName);

    void PostMoveItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName, int hrMove, IntPtr psiNewlyCreated);

    void PreCopyItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName);

    void PostCopyItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName, int hrCopy, IntPtr psiNewlyCreated);

    // PreserveSig, and returning a failure HRESULT, is the only way to stop a running
    // PerformOperations: without PreserveSig the CLR treats an int return as a retval parameter and
    // passes an argument the shell does not expect on that slot.
    [PreserveSig]
    int PreDeleteItem(uint dwFlags, IShellItem psiItem);

    void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IntPtr psiNewlyCreated);

    void PreNewItem(uint dwFlags, IntPtr psiDestinationFolder, IntPtr pszNewName);

    void PostNewItem(uint dwFlags, IntPtr psiDestinationFolder, IntPtr psiNewItem, IntPtr pszNewName, int hrNew, IntPtr psiNewlyCreated);

    void UpdateProgress(int iWorkTotal, int iWorkSoFar);

    void ResetTimer();

    void PauseTimer();

    void ResumeTimer();
}

[ComImport]
[Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    void Advise([MarshalAs(UnmanagedType.Interface)] IFileOperationProgressSink pfops, out uint cookie);

    void Unadvise(uint cookie);

    void SetOperationFlags(uint fileOperationFlags);

    // Slot 4. This is SetProgressMessage, NOT a SetOperationFlags2 overload - declaring a method
    // that does not exist here shifts every later slot and crashes with 0xC0000005.
    // Unused: the shell returns E_NOTIMPL and FOF_SILENT hides the progress dialog anyway.
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);

    void SetProgressDialog(IntPtr progressDialog);

    void SetProperties(IntPtr properties);

    void SetOwnerWindow(IntPtr ownerWindow);

    void ApplyPropertiesToItem(IntPtr item, IntPtr properties);

    void ApplyPropertiesToItems(IntPtr items, IntPtr properties);

    void RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr perItemSink);

    void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string newName);

    void MoveItem(IntPtr item, IntPtr destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr perItemSink);

    void MoveItems(IntPtr items, IntPtr destinationFolder);

    void CopyItem(IntPtr item, IntPtr destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, IntPtr perItemSink);

    void CopyItems(IntPtr items, IntPtr destinationFolder);

    void DeleteItem([MarshalAs(UnmanagedType.Interface)] IShellItem item, IntPtr perItemSink);

    void DeleteItems(IntPtr items);

    void NewItem(
        IntPtr destinationFolder,
        uint fileAttributes,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [MarshalAs(UnmanagedType.LPWStr)] string templateName,
        IntPtr perItemSink,
        out IntPtr newItem);

    void PerformOperations();

    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
}

internal static class ShellNative
{
    internal static readonly Guid ClsidFileOperation = new("3AD05575-8857-4850-9277-11B85BDB8E09");
    internal static readonly Guid IidIFileOperation = new("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8");
    internal static readonly Guid IidIShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    internal const uint CLSCTX_INPROC_SERVER = 0x1;
    internal const uint COINIT_APARTMENTTHREADED = 0x2;

    [DllImport("ole32.dll", PreserveSig = false)]
    internal static extern void CoCreateInstance(
        ref Guid classId,
        IntPtr outer,
        uint context,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IFileOperation fileOperation);

    [DllImport("ole32.dll", PreserveSig = false)]
    internal static extern void CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll", PreserveSig = false)]
    internal static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr memory);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    internal static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
}