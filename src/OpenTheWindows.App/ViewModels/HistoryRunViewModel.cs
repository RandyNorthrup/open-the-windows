using System.Globalization;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One prior run in the History list, with whether it can be reverted.</summary>
internal sealed class HistoryRunViewModel
{
    /// <summary>Projects a journal summary.</summary>
    public HistoryRunViewModel(JournalSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        RunId = summary.RunId;
        StartedAtLabel = summary.StartedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        Profile = summary.Profile;
        ResultLabel = DisplayLabels.For(summary.Result);
        EntryCount = summary.EntryCount;
        IsRevert = summary.RevertOf is not null;
        CanRevert = summary.Result == RunState.Completed && !IsRevert;
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

    /// <summary>True when this run can be reverted (a completed, non-revert run).</summary>
    public bool CanRevert { get; }
}
