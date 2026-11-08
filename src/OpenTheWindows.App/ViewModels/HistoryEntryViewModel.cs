using System.Globalization;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One entry of a prior run in the History detail pane: its id, outcome, risk and the actions it recorded.</summary>
internal sealed class HistoryEntryViewModel
{
    /// <summary>Projects one journal entry.</summary>
    public HistoryEntryViewModel(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Id = entry.Id;
        ResultLabel = DisplayLabels.For(entry.Result);
        RiskLabel = DisplayLabels.For(entry.Risk);
        ActionCount = entry.Actions.Count;
        CanRevert = entry.Result == ApplyState.Applied;
    }

    /// <summary>The catalogue entry id.</summary>
    public string Id { get; }

    /// <summary>The entry's outcome in the run.</summary>
    public string ResultLabel { get; }

    /// <summary>The entry's risk tier.</summary>
    public string RiskLabel { get; }

    /// <summary>How many actions the entry recorded.</summary>
    public int ActionCount { get; }

    /// <summary>True when the entry was applied, so reverting it alone has something to put back.</summary>
    public bool CanRevert { get; }

    /// <summary>Spoken by screen readers for the list item.</summary>
    public override string ToString() => string.Create(CultureInfo.CurrentCulture, $"{Id}, {ResultLabel}, {RiskLabel}");
}
