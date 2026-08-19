namespace OpenTheWindows.Core.Audit;

/// <summary>A one-line summary of a baseline for <c>otw audit list</c>.</summary>
/// <param name="Id">The baseline id.</param>
/// <param name="Title">The baseline's human title.</param>
/// <param name="RuleCount">Number of rules in the baseline.</param>
public sealed record BaselineSummary(string Id, string Title, int RuleCount);
