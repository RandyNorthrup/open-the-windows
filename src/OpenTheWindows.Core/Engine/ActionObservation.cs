using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>The observed compliance of a single action.</summary>
/// <param name="Action">The action that was evaluated.</param>
/// <param name="State">Compliance result.</param>
/// <param name="Actual">Human-readable observed state.</param>
/// <param name="Desired">Human-readable desired state.</param>
/// <param name="Managed">Whether (and how) the setting is managed by an organisation.</param>
public sealed record ActionObservation(
    ITweakAction Action,
    ComplianceState State,
    string Actual,
    string Desired,
    ManagedState Managed);
