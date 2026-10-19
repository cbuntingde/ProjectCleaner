namespace ProjectCleaner.Models;

/// <summary>How a delete should be carried out.</summary>
public enum DeleteMode
{
    /// <summary>Send to the Windows Recycle Bin - recoverable.</summary>
    RecycleBin,

    /// <summary>Unrecoverable erase, straight off the disk.</summary>
    Permanent,
}

public sealed record DeleteOutcome(string Path, bool Succeeded, string? Error);

/// <summary>Per-path results plus the totals the status bar reports back to the user.</summary>
public sealed class DeleteResult
{
    public required DeleteMode Mode { get; init; }

    public required IReadOnlyList<DeleteOutcome> Outcomes { get; init; }

    public int SucceededCount => Outcomes.Count(o => o.Succeeded);

    public int FailedCount => Outcomes.Count(o => !o.Succeeded);

    public IReadOnlyList<DeleteOutcome> Failures => Outcomes.Where(o => !o.Succeeded).ToArray();
}