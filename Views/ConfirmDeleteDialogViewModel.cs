using MaterialDesignThemes.Wpf;
using ProjectCleaner.Infrastructure;
using ProjectCleaner.Models;

namespace ProjectCleaner.Views;

/// <summary>What the confirm dialog needs to describe the pending delete.</summary>
public sealed record ConfirmationRequest
{
    public required string Headline { get; init; }

    public required string Detail { get; init; }

    public required string ConfirmLabel { get; init; }

    /// <summary>Renders the confirm button in the error colour for irreversible actions.</summary>
    public bool IsDestructive { get; init; }

    public required IReadOnlyList<string> SamplePaths { get; init; }
}

/// <summary>Drives the pre-delete confirmation, including the recoverable / unrecoverable framing.</summary>
public sealed class ConfirmDeleteDialogViewModel : ObservableObject
{
    /// <summary>How many paths to spell out before collapsing to "...and N more".</summary>
    private const int SampleSize = 8;

    public ConfirmDeleteDialogViewModel(
        ConfirmationRequest request,
        long totalBytes,
        int selectedCount,
        object dialogHostIdentifier)
    {
        Headline = request.Headline;
        Detail = request.Detail;
        ConfirmLabel = request.ConfirmLabel;
        IsDestructive = request.IsDestructive;
        SelectedCount = selectedCount;
        TotalSizeDisplay = ByteFormatter.Format(totalBytes);

        var paths = request.SamplePaths.ToArray();
        var shown = paths.Take(SampleSize).ToList();

        if (paths.Length > shown.Count)
        {
            shown.Add($"... and {paths.Length - shown.Count:N0} more");
        }

        SampleLines = shown;

        // Close(dialogIdentifier, parameter) - the single-argument overload takes the identifier,
        // not the result, so Close(true) would look for a host named "True".
        ConfirmCommand = new RelayCommand(() => DialogHost.Close(dialogHostIdentifier, true));
        CancelCommand = new RelayCommand(() => DialogHost.Close(dialogHostIdentifier, false));
    }

    public string Headline { get; }

    public string Detail { get; }

    public string ConfirmLabel { get; }

    /// <summary>True when the action cannot be undone - drives the red confirm button.</summary>
    public bool IsDestructive { get; }

    public int SelectedCount { get; }

    public string TotalSizeDisplay { get; }

    public IReadOnlyList<string> SampleLines { get; }

    public string SummaryDisplay => SelectedCount == 1
        ? "1 item"
        : $"{SelectedCount:N0} items";

    public RelayCommand ConfirmCommand { get; }

    public RelayCommand CancelCommand { get; }
}