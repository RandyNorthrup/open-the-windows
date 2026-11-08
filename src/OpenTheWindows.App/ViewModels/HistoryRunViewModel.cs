using System.Globalization;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// One prior run in the History list: when, what, its outcome, whether its journal
/// verifies, whether a later run reverted it, and whether it can be reverted now.
/// </summary>
internal sealed class HistoryRunViewModel
{
    /// <summary>Projects a journal summary; <paramref name="reverted"/> marks a run a later completed revert undid.</summary>
    public HistoryRunViewModel(JournalSummary summary, bool reverted = false)
    {
        ArgumentNullException.ThrowIfNull(summary);
        RunId = summary.RunId;
        StartedAtLabel = summary.StartedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        Profile = summary.Profile;
        ResultLabel = DisplayLabels.For(summary.Result);
        EntryCount = summary.EntryCount;
        IsRevert = summary.RevertOf is not null;
        IsReverted = reverted;
        Integrity = summary.Integrity;
        IntegrityLabel = DisplayLabels.For(summary.Integrity);
        IsTrusted = summary.Integrity is JournalIntegrity.Verified or JournalIntegrity.Unverified;
        CanRevert = summary.Result is RunState.Completed or RunState.Failed && !IsRevert && IsTrusted;
    }

    /// <summary>The run id.</summary>
    public Guid RunId { get; }

    /// <summary>When the run started (local time).</summary>
    public string StartedAtLabel { get; }

    /// <summary>The profile/label the run applied.</summary>
    public string Profile { get; }

    /// <summary>The run's final state.</summary>
    public string ResultLabel { get; }

    /// <summary>How many entries the run touched.</summary>
    public int EntryCount { get; }

    /// <summary>True when this run is itself a revert of another run.</summary>
    public bool IsRevert { get; }

    /// <summary>True when a later completed revert undid some or all of this run.</summary>
    public bool IsReverted { get; }

    /// <summary>Whether the run's journal verifies.</summary>
    public JournalIntegrity Integrity { get; }

    /// <summary>The localised integrity label.</summary>
    public string IntegrityLabel { get; }

    /// <summary>True when the journal can be trusted for revert (it verifies, or verification is deferred to the revert).</summary>
    public bool IsTrusted { get; }

    /// <summary>True when the journal failed verification or could not be read (it is never replayed).</summary>
    public bool IsUntrusted => !IsTrusted;

    /// <summary>
    /// True when this run can be reverted: an apply run (not a revert) that completed, or
    /// failed part-way, with a trusted journal. The engine still skips entries already
    /// reverted and refuses when a later run changed the same settings.
    /// </summary>
    public bool CanRevert { get; }

    /// <summary>Spoken by screen readers for the list item.</summary>
    public override string ToString() => string.Create(CultureInfo.CurrentCulture,
        $"{StartedAtLabel}, {Profile}, {ResultLabel}, {EntryCount} entries{(IsReverted ? ", reverted" : string.Empty)}{(IsTrusted ? string.Empty : ", " + IntegrityLabel)}");
}
