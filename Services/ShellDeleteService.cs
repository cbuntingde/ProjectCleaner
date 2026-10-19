using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ProjectCleaner.Interop;
using ProjectCleaner.Models;

namespace ProjectCleaner.Services;

public interface IDeleteService
{
    Task<DeleteResult> DeleteAsync(
        IReadOnlyList<CleanupTarget> targets,
        DeleteMode mode,
        IntPtr ownerWindow,
        IProgress<int>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// Deletes via <c>IFileOperation</c>, which is the supported way to reach the Recycle Bin and to get
/// correct handling of long paths, elevation prompts and oversized items.
///
/// Runs on a dedicated STA thread: the shell operation is apartment-affine, and a large folder tree
/// must not block the WPF dispatcher while it is being removed.
/// </summary>
public sealed class ShellDeleteService : IDeleteService
{
    // FILEOPERATION_FLAGS
    private const uint FOF_SILENT = 0x0004;
    private const uint FOF_NOCONFIRMATION = 0x0010;
    private const uint FOF_ALLOWUNDO = 0x0040;      // send to the Recycle Bin
    private const uint FOF_NOCONFIRMMKDIR = 0x0200;
    private const uint FOF_NOERRORUI = 0x0400;
    private const uint FOFX_SHOWELEVATIONPROMPT = 0x00040000;

    public Task<DeleteResult> DeleteAsync(
        IReadOnlyList<CleanupTarget> targets,
        DeleteMode mode,
        IntPtr ownerWindow,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var paths = targets.Select(t => t.FullPath).ToArray();

        var completion = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        // A dedicated STA thread, not the pool: thread-pool threads frequently arrive with COM
        // already initialised as MTA (RPC_E_CHANGED_MODE), and IFileOperation wants an STA.
        // The thread is background so a stuck delete can never hold the process open.
        var worker = new Thread(() =>
        {
            var ownsCom = false;

            try
            {
                try
                {
                    ShellNative.CoInitializeEx(IntPtr.Zero, ShellNative.COINIT_APARTMENTTHREADED);
                    ownsCom = true;
                }
                catch (COMException)
                {
                    // Already initialised in some other mode on this thread; carry on regardless.
                }

                completion.SetResult(Execute(paths, mode, ownerWindow, progress, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                if (ownsCom)
                {
                    ShellNative.CoUninitialize();
                }
            }
        })
        {
            IsBackground = true,
            Name = "ProjectCleaner delete",
        };

        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();

        return completion.Task;
    }

    private static DeleteResult Execute(
        IReadOnlyList<string> paths,
        DeleteMode mode,
        IntPtr ownerWindow,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var sink = new DeleteProgressSink(progress, cancellationToken);
        var notQueued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directFailures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var forShell = new List<string>(paths.Count);
        var attempted = new List<string>(paths.Count);

        // Permanent deletes go to the filesystem first. The shell charges a flat ~95 ms for every
        // directory queued into it - measured, and unaffected by flags, mode, sink or thread count -
        // which swamps a scan that matches many small folders: 200 empty folders took 20.6 s through
        // the shell and 48 ms through Directory.Delete. Anything the filesystem API refuses (a
        // junction or a read-only file inside the tree, a lock, a permission, a name the API cannot
        // represent) is handed to the shell instead, which is what copes with those.
        foreach (var path in paths)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            attempted.Add(path);

            if (mode != DeleteMode.Permanent)
            {
                forShell.Add(path);
                continue;
            }

            if (TryDeleteDirectly(path, out var failure))
            {
                sink.Advance();
                continue;
            }

            directFailures[path] = failure;
            forShell.Add(path);
        }

        if (forShell.Count > 0)
        {
            PerformShellDelete(forShell, mode, ownerWindow, sink, notQueued, cancellationToken);
        }

        return CollectOutcomes(attempted, mode, sink, notQueued, directFailures);
    }

    /// <summary>
    /// Removes one item with the filesystem API. Returns false only when the API refused it, in
    /// which case <paramref name="failure"/> says why and the caller falls back to the shell.
    /// </summary>
    private static bool TryDeleteDirectly(string path, out string failure)
    {
        try
        {
            // GetAttributes rather than Directory.Exists/File.Exists: those swallow access-denied
            // and report a folder the process cannot open as simply absent, which would have us
            // claim a delete that never happened. This throws instead, so it lands in the fallback.
            var attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }

            failure = string.Empty;
            return true;
        }
        catch (FileNotFoundException)
        {
            // Gone since the scan - nothing to do, and not a failure.
            failure = string.Empty;
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            failure = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            failure = $"the filesystem API refused it ({ex.Message})";
            return false;
        }
    }

    private static void PerformShellDelete(
        IReadOnlyList<string> paths,
        DeleteMode mode,
        IntPtr ownerWindow,
        DeleteProgressSink sink,
        HashSet<string> notQueued,
        CancellationToken cancellationToken)
    {
        var classId = ShellNative.ClsidFileOperation;
        var interfaceId = ShellNative.IidIFileOperation;

        ShellNative.CoCreateInstance(
            ref classId,
            IntPtr.Zero,
            ShellNative.CLSCTX_INPROC_SERVER,
            ref interfaceId,
            out var operation);

        try
        {
            operation.SetOperationFlags(BuildFlags(mode));

            // No SetProgressMessage call: the shell returns E_NOTIMPL for it, and FOF_SILENT
            // suppresses the shell's own progress dialog anyway. The status bar drives the UI.

            operation.SetOwnerWindow(ownerWindow);
            operation.Advise(sink, out _);

            var queued = 0;

            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var shellItemId = ShellNative.IidIShellItem;

                if (ShellNative.SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemId, out var item) != 0 || item is null)
                {
                    // Vanished since the scan, or the shell cannot represent it.
                    notQueued.Add(path);
                    continue;
                }

                try
                {
                    operation.DeleteItem(item, IntPtr.Zero);
                    queued++;
                }
                catch (COMException)
                {
                    notQueued.Add(path);
                }
                finally
                {
                    TryRelease(item);
                }
            }

            if (queued == 0)
            {
                return;
            }

            try
            {
                operation.PerformOperations();
            }
            catch (COMException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected, and the only way to stop a running operation: the sink refused the
                // next item, so the shell aborted and surfaced that as a failure HRESULT.
                // Whatever was already removed still shows up through the existence check below.
            }
        }
        finally
        {
            TryRelease(operation);
        }
    }

    private static DeleteResult CollectOutcomes(
        IReadOnlyList<string> attempted,
        DeleteMode mode,
        DeleteProgressSink sink,
        HashSet<string> notQueued,
        Dictionary<string, string> directFailures)
    {
        // Existence is the source of truth: the shell occasionally reports success for work it
        // only queued, and a locked file can survive with an ambiguous HRESULT.
        var outcomes = new List<DeleteOutcome>(attempted.Count);

        foreach (var path in attempted)
        {
            bool stillThere = File.Exists(path) || Directory.Exists(path);

            outcomes.Add(stillThere
                ? new DeleteOutcome(path, false, DescribeFailure(path, sink, notQueued, directFailures))
                : new DeleteOutcome(path, true, null));
        }

        return new DeleteResult { Mode = mode, Outcomes = outcomes };
    }

    /// <summary>
    /// Recycle mode deliberately omits FOF_NOCONFIRMATION and FOF_NOERRORUI: when an item is too
    /// big for the Recycle Bin, Windows asks the user rather than quietly erasing it. Permanent
    /// mode sets them, because the user already confirmed in our own dialog.
    /// </summary>
    private static uint BuildFlags(DeleteMode mode) => mode == DeleteMode.RecycleBin
        ? FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | FOF_SILENT | FOFX_SHOWELEVATIONPROMPT
        : FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_NOCONFIRMMKDIR | FOF_SILENT | FOFX_SHOWELEVATIONPROMPT;

    private static string DescribeFailure(string path, DeleteProgressSink sink, HashSet<string> notQueued, Dictionary<string, string> directFailures)
    {
        if (notQueued.Contains(path))
        {
            return "could not be opened for deletion - it may have moved or vanished since the scan";
        }

        if (sink.TryGetHResult(path, out var hresult) && hresult != 0)
        {
            return $"refused by the shell - {FormatHResult(hresult)}";
        }

        if (directFailures.TryGetValue(path, out var directFailure))
        {
            return $"still present after the delete - {directFailure}";
        }

        return "still present after the delete - likely in use, read-only, or permission denied";
    }

    private static string FormatHResult(int hresult)
    {
        try
        {
            string message = Marshal.GetExceptionForHR(hresult)?.Message ?? "no description";
            return $"{hresult:X8} ({message})";
        }
        catch (Exception)
        {
            return hresult.ToString("X8");
        }
    }

    private static void TryRelease(object comObject)
    {
        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch (Exception)
        {
            // Best effort only - the runtime will reclaim it regardless.
        }
    }

    /// <summary>Captures the shell's per-item HRESULT so failures can be explained.</summary>
    [ComVisible(true)]
    private sealed class DeleteProgressSink : IFileOperationProgressSink
    {
        private const int Cancelled = unchecked((int)0x800704C7);   // HRESULT_FROM_WIN32(ERROR_CANCELLED)

        private readonly ConcurrentDictionary<string, int> _results = new(StringComparer.OrdinalIgnoreCase);
        private readonly IProgress<int>? _progress;
        private readonly CancellationToken _cancellationToken;

        private int _completed;

        internal DeleteProgressSink(IProgress<int>? progress, CancellationToken cancellationToken)
        {
            _progress = progress;
            _cancellationToken = cancellationToken;
        }

        /// <summary>Counts an item the filesystem API removed before the shell was ever involved.</summary>
        internal void Advance() => _progress?.Report(Interlocked.Increment(ref _completed));

        internal bool TryGetHResult(string path, out int hresult) => _results.TryGetValue(path, out hresult);

        /// <summary>
        /// IFileOperation has no abort entry point, so refusing the next item is the only way to
        /// stop a running PerformOperations. It comes back as a failure HRESULT.
        /// </summary>
        public int PreDeleteItem(uint dwFlags, IShellItem psiItem)
            => _cancellationToken.IsCancellationRequested ? Cancelled : 0;

        public void StartOperations()
        {
        }

        public void FinishOperations(int hrResult)
        {
        }

        public void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IntPtr psiNewlyCreated)
        {
            var path = TryGetPath(psiItem);

            if (path is null)
            {
                Advance();
                return;
            }

            // Count each path once: the shell reports some items more than once - a folder reached
            // through a reparse point came back twice in testing - and this count drives a
            // "n of m" display that would run past m.
            if (_results.TryAdd(path, hrDelete))
            {
                Advance();
            }
        }

        public void UpdateProgress(int iWorkTotal, int iWorkSoFar)
        {
        }

        public void ResetTimer()
        {
        }

        public void PauseTimer()
        {
        }

        public void ResumeTimer()
        {
        }

        public void PreRenameItem(uint dwFlags, IShellItem psiItem, IntPtr pszNewName)
        {
        }

        public void PostRenameItem(uint dwFlags, IShellItem psiItem, IntPtr pszNewName, int hrRename, IntPtr psiNewlyCreated)
        {
        }

        public void PreMoveItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName)
        {
        }

        public void PostMoveItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName, int hrMove, IntPtr psiNewlyCreated)
        {
        }

        public void PreCopyItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName)
        {
        }

        public void PostCopyItem(uint dwFlags, IShellItem psiItem, IntPtr psiDestinationFolder, IntPtr pszNewName, int hrCopy, IntPtr psiNewlyCreated)
        {
        }

        public void PreNewItem(uint dwFlags, IntPtr psiDestinationFolder, IntPtr pszNewName)
        {
        }

        public void PostNewItem(uint dwFlags, IntPtr psiDestinationFolder, IntPtr psiNewItem, IntPtr pszNewName, int hrNew, IntPtr psiNewlyCreated)
        {
        }

        private static string? TryGetPath(IShellItem item)
        {
            const uint SIGDN_FILESYSPATH = 0x80058000;

            if (item is null)
            {
                return null;
            }

            try
            {
                item.GetDisplayName(SIGDN_FILESYSPATH, out var namePointer);

                if (namePointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(namePointer);
                }
                finally
                {
                    ShellNative.CoTaskMemFree(namePointer);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}