using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;
using ProjectCleaner.Models;

namespace ProjectCleaner.Services;

/// <summary>Snapshot of scan progress, published on the UI thread via <see cref="IProgress{T}"/>.</summary>
public sealed record ScanProgress(long FoldersVisited, int Matches, long BytesFound, string CurrentPath);

/// <summary>
/// Walks a directory tree looking for names that match the enabled patterns.
///
/// Two properties matter here:
///  - Reparse points (junctions and symlinks) are never followed, so the walk cannot escape the
///    chosen root and cannot loop forever.
///  - A matched folder is recorded and <em>not</em> descended into. That makes nested matches
///    (<c>bin/obj</c> inside a matched <c>bin</c>) impossible to double count, and it means a
///    200k-file <c>node_modules</c> is one cheap metadata call rather than a full subtree walk.
/// </summary>
public sealed class ScannerService
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(120);

    public Task<IReadOnlyList<CleanupTarget>> ScanAsync(
        string root,
        IReadOnlyList<CleanupPattern> patterns,
        IProgress<ScanProgress>? progress,
        IProgress<CleanupTarget>? onMatch,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => Scan(root, patterns, progress, onMatch, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<CleanupTarget> Scan(
        string root,
        IReadOnlyList<CleanupPattern> patterns,
        IProgress<ScanProgress>? progress,
        IProgress<CleanupTarget>? onMatch,
        CancellationToken cancellationToken)
    {
        var folderMatcher = new NameMatcher(patterns.Where(p => p.IsEnabled && p.Kind == PatternKind.Folder));
        var fileMatcher = new NameMatcher(patterns.Where(p => p.IsEnabled && p.Kind == PatternKind.File));

        if (!folderMatcher.HasMatches && !fileMatcher.HasMatches)
        {
            return [];
        }

        var results = new List<CleanupTarget>();
        long bytesFound = 0;
        long foldersVisited = 0;
        var clock = Stopwatch.StartNew();
        string currentPath = root;

        // Breadth via an explicit stack: deep trees must not blow the CLR stack.
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folder = pending.Pop();
            currentPath = folder;
            foldersVisited++;

            if (clock.Elapsed >= ReportInterval)
            {
                progress?.Report(new ScanProgress(foldersVisited, results.Count, bytesFound, currentPath));
                clock.Restart();
            }

            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(folder);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                continue; // unreadable folder - skip it rather than abort the whole scan
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or FileNotFoundException)
                {
                    continue;
                }

                // Never cross a junction or symlink: keeps the walk inside the selected root.
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var name = Path.GetFileName(entry);
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;

                if (isDirectory)
                {
                    if (folderMatcher.TryMatch(name, out var folderPattern))
                    {
                        Measure(entry, out long size, out int fileCount, out DateTime newest);
                        bytesFound += size;

                        var target = new CleanupTarget(
                            entry,
                            name,
                            PatternKind.Folder,
                            folderPattern.Match,
                            folderPattern.Category,
                            size,
                            fileCount,
                            newest);

                        results.Add(target);

                        // Publish immediately so the list fills as the walk proceeds.
                        onMatch?.Report(target);

                        // Deliberately do not descend - the whole subtree goes with the match.
                        continue;
                    }

                    pending.Push(entry);
                }
                else if (fileMatcher.TryMatch(name, out var filePattern))
                {
                    long size = 0;
                    DateTime newest = DateTime.MinValue;

                    try
                    {
                        var info = new FileInfo(entry);
                        size = info.Length;
                        newest = info.LastWriteTimeUtc;
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        // Keep the hit even if metadata is unreadable - it still matched.
                    }

                    bytesFound += size;

                    var fileTarget = new CleanupTarget(
                        entry,
                        name,
                        PatternKind.File,
                        filePattern.Match,
                        filePattern.Category,
                        size,
                        1,
                        newest);

                    results.Add(fileTarget);
                    onMatch?.Report(fileTarget);
                }
            }
        }

        progress?.Report(new ScanProgress(foldersVisited, results.Count, bytesFound, currentPath));

        return results
            .OrderByDescending(r => r.SizeBytes)
            .ThenBy(r => r.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Sums the sizes and counts the files under a matched folder.</summary>
    private static void Measure(string root, out long totalBytes, out int fileCount, out DateTime newestUtc)
    {
        totalBytes = 0;
        fileCount = 0;
        newestUtc = DateTime.MinValue;

        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(pending.Pop());
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or FileNotFoundException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                try
                {
                    var info = new FileInfo(entry);
                    totalBytes += info.Length;
                    fileCount++;

                    if (info.LastWriteTimeUtc > newestUtc)
                    {
                        newestUtc = info.LastWriteTimeUtc;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Unreadable file - it still counts towards the folder's presence, just not its size.
                }
            }
        }
    }

    /// <summary>Matches a single entry name against a set of literal names and globs.</summary>
    private sealed class NameMatcher
    {
        private readonly Dictionary<string, CleanupPattern> _literals = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string Glob, CleanupPattern Pattern)> _globs = [];

        public bool HasMatches => _literals.Count > 0 || _globs.Count > 0;

        public NameMatcher(IEnumerable<CleanupPattern> patterns)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.IsWildcard)
                {
                    _globs.Add((pattern.Match, pattern));
                }
                else
                {
                    // First rule wins if two enabled patterns name the same thing.
                    _literals.TryAdd(pattern.Match, pattern);
                }
            }
        }

        public bool TryMatch(string name, [NotNullWhen(true)] out CleanupPattern? pattern)
        {
            foreach (var (glob, globPattern) in _globs)
            {
                if (FileSystemName.MatchesSimpleExpression(glob, name, ignoreCase: true))
                {
                    pattern = globPattern;
                    return true;
                }
            }

            if (_literals.TryGetValue(name, out var literalPattern))
            {
                pattern = literalPattern;
                return true;
            }

            pattern = null;
            return false;
        }
    }
}