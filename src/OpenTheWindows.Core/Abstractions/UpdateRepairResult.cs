namespace OpenTheWindows.Core.Abstractions;

/// <summary>The outcome of a Windows Update repair.</summary>
/// <param name="Succeeded">Whether every step completed (services back to their prior running state).</param>
/// <param name="Steps">What was done (or, for a what-if, would be done), in order.</param>
/// <param name="SetAside">The folders renamed aside, which can be deleted once updates work again.</param>
/// <param name="Error">Why the repair stopped, when it did not succeed.</param>
public sealed record UpdateRepairResult(bool Succeeded, IReadOnlyList<string> Steps, IReadOnlyList<string> SetAside, string? Error);
