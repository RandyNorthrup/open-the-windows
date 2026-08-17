namespace OpenTheWindows.Core.Abstractions;

/// <summary>Observed state of a scheduled task.</summary>
/// <param name="Path">Full task path.</param>
/// <param name="Enabled">Whether the task is enabled (meaningful only when <see cref="Exists"/>).</param>
/// <param name="Exists">Whether the task is registered.</param>
public sealed record ScheduledTaskSnapshot(string Path, bool Enabled, bool Exists);
