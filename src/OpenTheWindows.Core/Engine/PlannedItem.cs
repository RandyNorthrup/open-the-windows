using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Internal pairing of a catalogue entry, its current scan observation and the
/// plan's disposition, ordered by dependency. Carries what both the public plan
/// projection and the apply loop need.
/// </summary>
/// <param name="Entry">The catalogue entry.</param>
/// <param name="Observation">The entry's current scan observation.</param>
/// <param name="Disposition">What the plan intends to do with it.</param>
/// <param name="Note">A human-readable explanation for a non-apply disposition.</param>
internal sealed record PlannedItem(
    TweakDefinition Entry,
    TweakObservation Observation,
    PlanDisposition Disposition,
    string? Note)
{
    /// <summary>Returns the same item with a different disposition and note.</summary>
    public PlannedItem As(PlanDisposition disposition, string? note)
        => this with { Disposition = disposition, Note = note };
}
