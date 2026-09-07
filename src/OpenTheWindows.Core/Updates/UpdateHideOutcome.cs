namespace OpenTheWindows.Core.Updates;

/// <summary>Result of hiding or unhiding one knowledge-base article.</summary>
/// <param name="Succeeded">Whether at least one matching update was changed.</param>
/// <param name="KnowledgeBaseId">The normalised KB number (digits only).</param>
/// <param name="Affected">How many discovered updates were changed.</param>
/// <param name="Message">Human-readable outcome; never contains secrets.</param>
public sealed record UpdateHideOutcome(bool Succeeded, string KnowledgeBaseId, int Affected, string Message);
