using System.Globalization;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One row of the what-if plan: an entry, what the run would do to it, and its actions.</summary>
internal sealed class ApplyPlanEntryViewModel
{
    /// <summary>Projects a planned entry.</summary>
    public ApplyPlanEntryViewModel(PlannedEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Id = entry.Id.Value;
        DispositionLabel = DisplayLabels.For(entry.Disposition);
        RiskLabel = DisplayLabels.For(entry.Risk);
        Note = entry.Note;
        WillApply = entry.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass;
        Actions = [.. entry.Actions.Select(a =>
            string.Create(CultureInfo.InvariantCulture, $"{a.Kind}: {a.Target} [{a.Current}]"))];
    }

    /// <summary>The entry id.</summary>
    public string Id { get; }

    /// <summary>What the run would do to this entry.</summary>
    public string DispositionLabel { get; }

    /// <summary>The entry's risk label.</summary>
    public string RiskLabel { get; }

    /// <summary>An explanatory note (why blocked / managed / refused), if any.</summary>
    public string? Note { get; }

    /// <summary>True when this entry would actually change the machine.</summary>
    public bool WillApply { get; }

    /// <summary>The per-action lines (kind, target and current compliance state).</summary>
    public IReadOnlyList<string> Actions { get; }
}
