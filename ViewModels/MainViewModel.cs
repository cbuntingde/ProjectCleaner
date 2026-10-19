using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using ProjectCleaner.Infrastructure;
using ProjectCleaner.Models;
using ProjectCleaner.Services;

namespace ProjectCleaner.ViewModels;

/// <summary>
/// Drives the window: choose a root, choose patterns, scan, tick what to remove, then delete.
/// All long-running work is off the dispatcher thread; only the result hand-back touches it.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>
    /// The DialogHost's Identifier. DialogHost.Show/Close resolve the host by matching this value,
    /// so the XAML and every caller must agree on it - hence one constant.
    /// </summary>
    public const string DialogHostIdentifier = "ProjectCleanerRoot";

    private readonly ScannerService _scanner = new();
    private readonly IDeleteService _deleteService;

    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _deleteCancellation;
    private int _foldersVisited;
    private string? _rootPath;
    private bool _isBusy;
    private bool _isScanning;
    private bool _isDeleting;
    private bool _suspendCollectionHandling;
    private long _totalBytes;
    private long _selectedBytes;
    private bool _hasScanned;
    private bool _justDeleted;
    private string _statusText = "Pick a folder to scan.";
    private string _currentPath = string.Empty;

    public MainViewModel(IDeleteService deleteService, Func<IntPtr> ownerWindowAccessor)
    {
        _deleteService = deleteService;
        _ownerWindowAccessor = ownerWindowAccessor;

        Patterns = new ObservableCollection<CleanupPattern>(PatternCatalog.CreateAll());
        Results = new ObservableCollection<CleanupTarget>();

        foreach (var pattern in Patterns)
        {
            pattern.PropertyChanged += OnPatternChanged;
        }

        Results.CollectionChanged += OnResultsChanged;

        BrowseCommand = new RelayCommand(Browse, () => !IsBusy);
        ScanCommand = new RelayCommand(Scan, () => CanScan);
        CancelCommand = new RelayCommand(CancelWork, () => IsScanning || IsDeleting);
        RecycleCommand = new RelayCommand(() => DeleteSelected(DeleteMode.RecycleBin), () => CanDelete);
        DeletePermanentlyCommand = new RelayCommand(() => DeleteSelected(DeleteMode.Permanent), () => CanDelete);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true), () => CanSelect);
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false), () => CanSelect);
        InvertSelectionCommand = new RelayCommand(InvertSelection, () => CanSelect);
        SetAllPatternsCommand = new RelayCommand(parameter => SetAllPatterns(parameter is true));
        ResetPatternsCommand = new RelayCommand(ResetPatterns);
    }

    private readonly Func<IntPtr> _ownerWindowAccessor;

    public ObservableCollection<CleanupPattern> Patterns { get; }

    public ObservableCollection<CleanupTarget> Results { get; }

    public RelayCommand BrowseCommand { get; }

    public RelayCommand ScanCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand RecycleCommand { get; }

    public RelayCommand DeletePermanentlyCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public RelayCommand InvertSelectionCommand { get; }

    public RelayCommand SetAllPatternsCommand { get; }

    public RelayCommand ResetPatternsCommand { get; }

    public string? RootPath
    {
        get => _rootPath;
        set
        {
            if (SetProperty(ref _rootPath, value))
            {
                OnPropertyChanged(nameof(HasRootPath));
                RefreshCommandStates();
            }
        }
    }

    public bool HasRootPath => !string.IsNullOrWhiteSpace(RootPath) && Directory.Exists(RootPath);

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                // Without this the Stop button's CanExecute is evaluated once, while scanning is
                // still false, and stays disabled for the whole scan.
                RefreshCommandStates();
            }
        }
    }

    public bool IsDeleting
    {
        get => _isDeleting;
        private set
        {
            if (SetProperty(ref _isDeleting, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                // The right-hand panel swaps between scanning / results / empty on this flag.
                RefreshSummaries();
            }
        }
    }

    public bool HasScanned
    {
        get => _hasScanned;
        private set
        {
            if (SetProperty(ref _hasScanned, value))
            {
                RefreshSummaries();
            }
        }
    }

    /// <summary>Streams live while a scan runs, so it is shown whenever anything has been found.</summary>
    public bool ShowResultsPanel => Results.Count > 0;

    /// <summary>Only once a scan has finished - never mid-scan, when "nothing found" would be a lie.</summary>
    public bool ShowEmptyState => HasScanned && !IsBusy && Results.Count == 0;

    /// <summary>A scan is under way and has not produced anything to show yet.</summary>
    public bool ShowScanningState => IsBusy && Results.Count == 0;

    /// <summary>Nothing has been scanned yet - the panel would otherwise be blank on first launch.</summary>
    public bool ShowWelcomeState => !HasScanned && !IsBusy;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public int EnabledPatternCount => Patterns.Count(p => p.IsEnabled);

    public bool CanScan => HasRootPath && !IsBusy;

    public bool CanDelete => !IsBusy && Results.Any(r => r.IsSelected);

    /// <summary>
    /// Select All/None/Invert need results to act on, not an existing selection - gating them on
    /// CanDelete left "All" disabled after "None", with no way back.
    /// </summary>
    public bool CanSelect => !IsBusy && Results.Count > 0;

    public int SelectedCount => Results.Count(r => r.IsSelected);

    /// <summary>Maintained incrementally - recomputing this per tick would be O(n) per keystroke.</summary>
    public long SelectedBytes => _selectedBytes;

    public long TotalBytes => _totalBytes;

    public string SelectedBytesDisplay => ByteFormatter.Format(SelectedBytes);

    public string TotalBytesDisplay => ByteFormatter.Format(TotalBytes);

    public string SelectionSummary => SelectedCount switch
    {
        0 => "Nothing ticked",
        1 => "1 item ticked",
        _ => $"{SelectedCount:N0} items ticked",
    };

    public string HeaderSummary => Results.Count == 0
        ? "Results"
        : $"{Results.Count:N0} found  ·  {TotalBytesDisplay} total";

    public string EmptyMessage => EnabledPatternCount == 0
        ? "No patterns are enabled - tick at least one on the left."
        : _justDeleted
            ? "Everything found here has been removed. Scan again to look for more."
            : $"Nothing matching the {EnabledPatternCount} enabled pattern(s) was found under this folder.";

    // ---------------------------------------------------------------- commands

    private void Browse()
    {
        var picked = new FolderPicker().PickFolder(GetOwnerWindow(), RootPath);

        if (!string.IsNullOrWhiteSpace(picked))
        {
            RootPath = Path.GetFullPath(picked);
            StatusText = "Ready to scan.";
        }
    }

    private async void Scan()
    {
        if (!HasRootPath)
        {
            StatusText = "That folder does not exist any more.";
            return;
        }

        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();

        IsBusy = true;
        IsScanning = true;
        HasScanned = true;
        _justDeleted = false;
        SetFailures([]);
        Results.Clear();
        RefreshSummaries();
        StatusText = "Scanning...";
        CurrentPath = string.Empty;

        var progress = new Progress<ScanProgress>(report =>
        {
            _foldersVisited = (int)Math.Min(report.FoldersVisited, int.MaxValue);
            CurrentPath = report.CurrentPath;
            StatusText = $"{_foldersVisited:N0} folders walked  -  {Results.Count:N0} found, {ByteFormatter.Format(_totalBytes)}";
        });

        // Matches arrive on the dispatcher as they are discovered, so the list fills live.
        var onMatch = new Progress<CleanupTarget>(target =>
        {
            target.PropertyChanged += OnTargetChanged;
            Results.Add(target);
        });

        try
        {
            await _scanner.ScanAsync(RootPath!, Patterns, progress, onMatch, _scanCancellation.Token);

            SortResultsBySize();
            CurrentPath = string.Empty;

            StatusText = Results.Count == 0
                ? $"Scanned {_foldersVisited:N0} folders - no matches."
                : $"Scanned {_foldersVisited:N0} folders, found {Results.Count:N0} item(s) totalling {TotalBytesDisplay}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = Results.Count == 0
                ? "Scan cancelled."
                : $"Scan cancelled - kept {Results.Count:N0} result(s) found so far.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            IsBusy = false;
            RefreshSummaries();
        }
    }

    /// <summary>Stop covers both long jobs: a scan walk, and a delete that the shell is working through.</summary>
    private void CancelWork()
    {
        _scanCancellation?.Cancel();
        _deleteCancellation?.Cancel();
        StatusText = "Cancelling...";
    }

    private async void DeleteSelected(DeleteMode mode)
    {
        var selected = Results.Where(r => r.IsSelected).ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        // Refuse anything that is not strictly inside the scanned root, or the root itself.
        var root = RootPath?.TrimEnd('\\', '/') ?? string.Empty;
        var unsafePaths = selected
            .Where(t => !IsInsideRoot(t.FullPath, root))
            .ToArray();

        if (unsafePaths.Length > 0)
        {
            StatusText = $"Refused: {unsafePaths.Length} item(s) sit outside the scanned folder.";
            return;
        }

        var totalBytes = selected.Sum(t => t.SizeBytes);

        var confirmed = await ConfirmAsync(selected, totalBytes, mode);
        if (!confirmed)
        {
            StatusText = "Nothing was deleted.";
            return;
        }

        IsBusy = true;
        IsDeleting = true;
        HasScanned = false;
        SetFailures([]);

        var total = selected.Length;
        var verb = mode == DeleteMode.RecycleBin ? "Recycling" : "Deleting";
        var action = mode == DeleteMode.RecycleBin ? "recycled" : "deleted";

        StatusText = $"{verb} 0 of {total:N0}...";

        _deleteCancellation?.Dispose();
        _deleteCancellation = new CancellationTokenSource();
        var deleteCancellation = _deleteCancellation;

        // Streamed per item as it is removed, so the status bar counts instead of the shell
        // holding an indeterminate bar for the whole operation.
        var progress = new Progress<int>(done => StatusText = $"{verb} {done:N0} of {total:N0}...");

        try
        {
            var result = await _deleteService.DeleteAsync(
                selected,
                mode,
                GetOwnerWindow(),
                progress,
                deleteCancellation.Token);

            RemoveFromResults(result);

            // Only what actually went away counts as freed - that matters most when a delete is
            // stopped part-way and most of the selection is still on disk.
            var sizeByPath = selected.ToDictionary(t => t.FullPath, t => t.SizeBytes, StringComparer.OrdinalIgnoreCase);
            var freedBytes = result.Outcomes
                .Where(o => o.Succeeded)
                .Sum(o => sizeByPath.TryGetValue(o.Path, out var size) ? size : 0);

            var freed = ByteFormatter.Format(freedBytes);

            if (deleteCancellation.IsCancellationRequested)
            {
                StatusText = result.SucceededCount == 0
                    ? "Stopped - nothing was removed."
                    : $"Stopped - {result.SucceededCount:N0} of {total:N0} item(s) {action}, {freed} freed.";
            }
            else
            {
                StatusText = result.FailedCount == 0
                    ? $"{result.SucceededCount:N0} item(s) {action}, {freed} freed."
                    : $"{result.SucceededCount:N0} {action}, {result.FailedCount:N0} failed - see below.";
            }

            _justDeleted = result.SucceededCount > 0;

            // A stopped delete leaves everything it had not reached in the list, so listing those
            // as failures would bury the real ones; the status bar already says it was stopped.
            SetFailures(deleteCancellation.IsCancellationRequested
                ? []
                : result.Failures.Select(f => $"{f.Path}  -  {f.Error}").ToArray());
        }
        catch (Exception ex)
        {
            StatusText = $"Delete failed: {ex.Message}";
        }
        finally
        {
            IsDeleting = false;
            IsBusy = false;
            HasScanned = true;
            RefreshSummaries();
        }
    }

    /// <summary>Per-path failure text, shown under the results when a delete could not be completed.</summary>
    public IReadOnlyList<string> Failures { get; private set; } = [];

    public bool HasFailures => Failures.Count > 0;

    private void SetFailures(IReadOnlyList<string> failures)
    {
        Failures = failures;
        OnPropertyChanged(nameof(Failures));
        OnPropertyChanged(nameof(HasFailures));
    }

    private async Task<bool> ConfirmAsync(IReadOnlyList<CleanupTarget> selected, long totalBytes, DeleteMode mode)
    {
        if (DialogHostReference is null)
        {
            return false;
        }

        var recycle = mode == DeleteMode.RecycleBin;

        var request = new Views.ConfirmationRequest
        {
            Headline = recycle
                ? $"Move {selected.Count:N0} item(s) to the Recycle Bin?"
                : $"Permanently delete {selected.Count:N0} item(s)?",
            Detail = recycle
                ? "These go to the Windows Recycle Bin and can be restored from there."
                : "This is immediate and cannot be undone. Anything held in the Recycle Bin stays recoverable; this does not.",
            ConfirmLabel = recycle ? "Recycle" : "Delete forever",
            IsDestructive = !recycle,
            SamplePaths = selected
                .OrderByDescending(t => t.SizeBytes)
                .Select(t => t.FullPath)
                .ToArray(),
        };

        var view = new Views.ConfirmDeleteDialog
        {
            DataContext = new Views.ConfirmDeleteDialogViewModel(
                request,
                totalBytes,
                selected.Count,
                DialogHostIdentifier)
        };

        // Resolve by Identifier, not by instance - DialogHost matches on the property.
        object? answer = await MaterialDesignThemes.Wpf.DialogHost.Show(view, DialogHostIdentifier);
        return answer is true;
    }

    /// <summary>The window's dialog host; confirmations are raised from here.</summary>
    public MaterialDesignThemes.Wpf.DialogHost? DialogHostReference { get; set; }

    private IntPtr GetOwnerWindow()
    {
        try
        {
            return _ownerWindowAccessor();
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    private static bool IsInsideRoot(string path, string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var normalized = path.TrimEnd('\\', '/');
        return normalized.Length > root.Length
            && normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && (normalized[root.Length] == '\\' || normalized[root.Length] == '/');
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Drops only what the delete confirmed gone. Anything the result never mentions - a stopped
    /// delete does not reach the tail of the selection - stays in the list.
    /// </summary>
    private void RemoveFromResults(DeleteResult result)
    {
        var removed = result.Outcomes
            .Where(o => o.Succeeded)
            .Select(o => o.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = Results.Count - 1; i >= 0; i--)
        {
            if (removed.Contains(Results[i].FullPath))
            {
                Results.RemoveAt(i);
            }
        }

        RecomputeTotals();
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var target in Results)
        {
            target.IsSelected = selected;
        }

        // Bulk change: let the per-item handler do the accounting, then raise the UI once.
        RecomputeSelectedTotals();
    }

    private void InvertSelection()
    {
        foreach (var target in Results)
        {
            target.IsSelected = !target.IsSelected;
        }

        RecomputeSelectedTotals();
    }

    /// <summary>Largest first. Suppresses the per-item collection churn while the list is rebuilt.</summary>
    private void SortResultsBySize()
    {
        var sorted = Results
            .OrderByDescending(r => r.SizeBytes)
            .ThenBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _suspendCollectionHandling = true;
        try
        {
            Results.Clear();

            foreach (var target in sorted)
            {
                Results.Add(target);
            }
        }
        finally
        {
            _suspendCollectionHandling = false;
        }

        RecomputeTotals();
    }

    private void OnResultsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_suspendCollectionHandling)
        {
            return;
        }

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add when e.NewItems is not null:
                foreach (CleanupTarget added in e.NewItems)
                {
                    _totalBytes += added.SizeBytes;

                    if (added.IsSelected)
                    {
                        _selectedBytes += added.SizeBytes;
                    }
                }

                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                RecomputeTotals();
                return;
        }

        RefreshSummaries();
    }

    private void RecomputeTotals()
    {
        _totalBytes = Results.Sum(r => r.SizeBytes);
        _selectedBytes = Results.Where(r => r.IsSelected).Sum(r => r.SizeBytes);
        RefreshSummaries();
    }

    private void RecomputeSelectedTotals()
    {
        _selectedBytes = Results.Where(r => r.IsSelected).Sum(r => r.SizeBytes);
        RefreshSummaries();
    }

    private void SetAllPatterns(bool enabled)
    {
        foreach (var pattern in Patterns)
        {
            pattern.IsEnabled = enabled;
        }

        RefreshPatternState();
    }

    private void ResetPatterns()
    {
        foreach (var pattern in Patterns)
        {
            pattern.IsEnabled = pattern.EnabledByDefault;
        }

        RefreshPatternState();
    }

    private void RefreshPatternState()
    {
        OnPropertyChanged(nameof(EnabledPatternCount));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void RefreshSummaries()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedBytesDisplay));
        OnPropertyChanged(nameof(TotalBytes));
        OnPropertyChanged(nameof(TotalBytesDisplay));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HeaderSummary));
        OnPropertyChanged(nameof(ShowResultsPanel));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowScanningState));
        OnPropertyChanged(nameof(ShowWelcomeState));
        OnPropertyChanged(nameof(EmptyMessage));

        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        BrowseCommand.RaiseCanExecuteChanged();
        ScanCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        RecycleCommand.RaiseCanExecuteChanged();
        DeletePermanentlyCommand.RaiseCanExecuteChanged();
        SelectAllCommand.RaiseCanExecuteChanged();
        SelectNoneCommand.RaiseCanExecuteChanged();
        InvertSelectionCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Re-checks <see cref="HasRootPath"/> when the window regains focus.</summary>
    public void RevalidateRoot()
    {
        OnPropertyChanged(nameof(HasRootPath));
        RefreshCommandStates();
    }

    private void OnPatternChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupPattern.IsEnabled))
        {
            RefreshPatternState();
        }
    }

    private void OnTargetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CleanupTarget.IsSelected) || sender is not CleanupTarget target)
        {
            return;
        }

        _selectedBytes += target.IsSelected ? target.SizeBytes : -target.SizeBytes;
        RefreshSummaries();
    }
}