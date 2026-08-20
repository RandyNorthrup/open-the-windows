using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// One catalogue entry as shown in a category list and detail pane: its
/// selection checkbox, risk / restart badges and the detail fields (rationale,
/// side effects, human-readable actions, sources). Read-only over the
/// <see cref="TweakDefinition"/>; only <see cref="IsSelected"/> is mutable.
/// </summary>
internal sealed partial class TweakItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Wraps a catalogue entry.</summary>
    public TweakItemViewModel(TweakDefinition entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Definition = entry;
        ActionSummaries = [.. entry.Actions.Select(a => string.Create(CultureInfo.InvariantCulture, $"{a.Kind}: {a}"))];
        Sources = [.. entry.Sources.Select(u => u.ToString())];
    }

    /// <summary>The entry this item wraps (for the apply/plan engine).</summary>
    public TweakDefinition Definition { get; }

    /// <summary>The entry id.</summary>
    public TweakId Id => Definition.Id;

    /// <summary>The entry id as text.</summary>
    public string IdText => Definition.Id.Value;

    /// <summary>The entry title.</summary>
    public string Title => Definition.Title;

    /// <summary>What the entry does.</summary>
    public string Description => Definition.Description;

    /// <summary>Why the entry exists.</summary>
    public string Rationale => Definition.Rationale;

    /// <summary>The risk tier.</summary>
    public RiskTier Risk => Definition.Risk;

    /// <summary>The localised risk label.</summary>
    public string RiskLabel => DisplayLabels.For(Definition.Risk);

    /// <summary>The minimum dial level that selects this entry.</summary>
    public Level Level => Definition.Level;

    /// <summary>The catalogue status (Draft / Verified / Deprecated).</summary>
    public TweakStatus Status => Definition.Status;

    /// <summary>The localised status label.</summary>
    public string StatusLabel => DisplayLabels.For(Definition.Status);

    /// <summary>What a change needs to take effect.</summary>
    public RestartRequirement Requires => Definition.Requires;

    /// <summary>True when applying needs a restart / sign-out / reboot.</summary>
    public bool RequiresRestart => Definition.Requires != RestartRequirement.None;

    /// <summary>The localised restart-requirement label.</summary>
    public string RestartLabel => DisplayLabels.For(Definition.Requires);

    /// <summary>The documented side effects.</summary>
    public IReadOnlyList<string> SideEffects => Definition.SideEffects;

    /// <summary>True when the entry has documented side effects.</summary>
    public bool HasSideEffects => Definition.SideEffects.Count > 0;

    /// <summary>Human-readable, one-line summaries of the entry's actions.</summary>
    public IReadOnlyList<string> ActionSummaries { get; }

    /// <summary>The source URLs as display strings.</summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>True when the entry is still a Draft (not yet verified for release).</summary>
    public bool IsDraft => Definition.Status == TweakStatus.Draft;

    /// <summary>True when <paramref name="term"/> matches the id, title, description, rationale or a tag.</summary>
    public bool Matches(string term) => IdText.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Title.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Rationale.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Definition.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase));
}
