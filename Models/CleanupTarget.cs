using ProjectCleaner.Infrastructure;

namespace ProjectCleaner.Models;

/// <summary>One thing the scan found that the user can tick and delete.</summary>
public sealed class CleanupTarget : ObservableObject
{
    public CleanupTarget(
        string fullPath,
        string name,
        PatternKind kind,
        string patternMatch,
        string category,
        long sizeBytes,
        int fileCount,
        DateTime lastWriteUtc)
    {
        FullPath = fullPath;
        Name = name;
        Kind = kind;
        PatternMatch = patternMatch;
        Category = category;
        SizeBytes = sizeBytes;
        FileCount = fileCount;
        LastWriteUtc = lastWriteUtc;
        IsSelected = true;
    }

    public string FullPath { get; }

    public string Name { get; }

    public PatternKind Kind { get; }

    public string PatternMatch { get; }

    public string Category { get; }

    public long SizeBytes { get; }

    public int FileCount { get; }

    public DateTime LastWriteUtc { get; }

    public bool IsDirectory => Kind == PatternKind.Folder;

    public string SizeDisplay => ByteFormatter.Format(SizeBytes);

    public string FilesDisplay => FileCount == 1 ? "1 file" : $"{FileCount:N0} files";

    public string LastWriteDisplay => LastWriteUtc == DateTime.MinValue
        ? "unknown"
        : LastWriteUtc.ToLocalTime().ToString("d MMM yyyy");

    private bool _isSelected;

    /// <summary>Tick state. Defaults to on so the common case is "scan, confirm, delete".</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}