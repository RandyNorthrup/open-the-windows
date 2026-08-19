using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One row of the apply result: an entry and what actually happened to it.</summary>
internal sealed class ApplyOutcomeEntryViewModel
{
    /// <summary>Projects an entry outcome.</summary>
    public ApplyOutcomeEntryViewModel(EntryOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Id = outcome.Id.Value;
        ResultLabel = DisplayLabels.For(outcome.Result);
        RiskLabel = DisplayLabels.For(outcome.Risk);
        Note = outcome.Note;
    }

    /// <summary>The entry id.</summary>
    public string Id { get; }

    /// <summary>What happened to this entry (applied, skipped, managed, rolled back, …).</summary>
    public string ResultLabel { get; }

    /// <summary>The entry's risk label.</summary>
    public string RiskLabel { get; }

    /// <summary>An explanatory note, if any.</summary>
    public string? Note { get; }
}
