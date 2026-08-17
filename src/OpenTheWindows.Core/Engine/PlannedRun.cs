namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Internal, fully classified and dependency-ordered plan the apply loop
/// consumes; projected to the public <see cref="PlanResult"/> for display.
/// </summary>
/// <param name="ProfileName">The profile the run is for.</param>
/// <param name="UserSid">The resolved interactive-user SID (for per-user hive writes).</param>
/// <param name="Items">The entries in apply order with their dispositions.</param>
/// <param name="Errors">Blocking plan errors (conflicts, dependency cycles).</param>
internal sealed record PlannedRun(
    string ProfileName,
    string? UserSid,
    IReadOnlyList<PlannedItem> Items,
    IReadOnlyList<string> Errors);
