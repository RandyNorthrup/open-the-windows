namespace OpenTheWindows.Core.Journal;

/// <summary>The interactive user a run's per-user changes targeted.</summary>
/// <param name="Sid">Security identifier of the target user.</param>
/// <param name="Name">DOMAIN\user display name.</param>
public sealed record JournalUser(string Sid, string Name);
