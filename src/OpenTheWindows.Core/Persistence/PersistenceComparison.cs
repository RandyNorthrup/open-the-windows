namespace OpenTheWindows.Core.Persistence;

/// <summary>The autostarts now compared with the baseline.</summary>
/// <param name="Baseline">The baseline compared against.</param>
/// <param name="Changes">What was added, removed or changed since (empty when nothing).</param>
public sealed record PersistenceComparison(PersistenceSnapshot Baseline, IReadOnlyList<PersistenceChange> Changes);
