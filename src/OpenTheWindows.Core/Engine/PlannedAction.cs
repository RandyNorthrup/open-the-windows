namespace OpenTheWindows.Core.Engine;

/// <summary>One action within a planned entry, with its current observed compliance.</summary>
/// <param name="Kind">The action kind discriminator.</param>
/// <param name="Target">What the action targets.</param>
/// <param name="Current">The action's current compliance state.</param>
public sealed record PlannedAction(string Kind, string Target, ComplianceState Current);
