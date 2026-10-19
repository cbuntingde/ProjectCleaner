using ProjectCleaner.Infrastructure;

namespace ProjectCleaner.Models;

/// <summary>Whether a pattern matches a folder, a file, or both.</summary>
public enum PatternKind
{
    Folder,
    File,
}

/// <summary>
/// One selectable "what to look for" rule. A pattern matches an entry whose <em>name</em> equals
/// <see cref="Match"/>, or matches it as a glob when <see cref="Match"/> contains <c>*</c> or <c>?</c>.
/// Matching is always case-insensitive and only ever applied to the entry name, never the full path.
/// </summary>
public sealed class CleanupPattern : ObservableObject
{
    public CleanupPattern(
        string category,
        string match,
        PatternKind kind,
        string description,
        bool enabledByDefault = true)
    {
        Category = category;
        Match = match;
        Kind = kind;
        Description = description;
        IsEnabled = enabledByDefault;
        EnabledByDefault = enabledByDefault;
    }

    public string Category { get; }

    public string Match { get; }

    public PatternKind Kind { get; }

    public string Description { get; }

    /// <summary>True when this pattern was part of the built-in default selection.</summary>
    public bool EnabledByDefault { get; }

    public bool IsWildcard => Match.IndexOfAny(['*', '?']) >= 0;

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string KindLabel => Kind == PatternKind.Folder ? "folder" : "file";

    public string MatchDisplay => IsWildcard ? Match : $"\"{Match}\"";
}