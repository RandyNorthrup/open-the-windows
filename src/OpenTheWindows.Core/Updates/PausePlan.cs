namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The result of a pause calculation: the exact registry values to write and the
/// start/expiry window they encode. Produced by <see cref="PauseCalculator"/> and
/// turned into a journaled, reversible apply run by the update-control service.
/// </summary>
/// <param name="Start">When the pause begins (now, UTC).</param>
/// <param name="Expiry">When the pause ends (now + <see cref="Days"/>, UTC).</param>
/// <param name="Days">Number of days paused.</param>
/// <param name="Extended">Whether the pause exceeds Microsoft's 35-day cap (Advanced risk).</param>
/// <param name="Values">The six <see cref="WindowsUpdateSettings.UxSettingsPath"/> values to write.</param>
public sealed record PausePlan(
    DateTimeOffset Start,
    DateTimeOffset Expiry,
    int Days,
    bool Extended,
    IReadOnlyList<PauseRegistryValue> Values);
