using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Journal;

/// <summary>
/// One journaled action. The descriptor fields (<see cref="Index"/>,
/// <see cref="Kind"/>, <see cref="Target"/>, <see cref="Action"/>,
/// <see cref="Prior"/>, <see cref="Desired"/>) are written before the change;
/// the outcome fields (<see cref="AppliedAt"/>, <see cref="Verified"/>,
/// <see cref="Result"/>) are updated as the action runs. The full
/// <see cref="Action"/> is stored so revert is self-contained: it can restore
/// the prior state from the journal alone, without re-reading the catalogue.
/// </summary>
public sealed class JournalAction
{
    /// <summary>Zero-based position of the action within its entry.</summary>
    public int Index { get; init; }

    /// <summary>Action kind discriminator (e.g. "Registry", "Service").</summary>
    public required string Kind { get; init; }

    /// <summary>Human- and machine-readable identifier of what the action targets.</summary>
    public required string Target { get; init; }

    /// <summary>The full catalogue action, so revert can be replayed from the journal alone.</summary>
    public required ITweakAction Action { get; init; }

    /// <summary>The state captured before the change; <see langword="null"/> when the kind has no revertible prior (e.g. commands).</summary>
    public JournalActionState? Prior { get; init; }

    /// <summary>The desired state the action writes (a compact, human-readable summary of <see cref="Action"/>).</summary>
    public required JournalActionState Desired { get; init; }

    /// <summary>When the action was applied (from <see cref="TimeProvider"/>); <see langword="null"/> until then.</summary>
    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>Whether the applied state was re-read and matched the desired state.</summary>
    public bool Verified { get; set; }

    /// <summary>The action's outcome.</summary>
    public ApplyState Result { get; set; }
}
