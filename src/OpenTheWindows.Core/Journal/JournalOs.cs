namespace OpenTheWindows.Core.Journal;

/// <summary>The OS identity recorded in a journal, so a run can be understood after the fact.</summary>
/// <param name="Build">Major build number.</param>
/// <param name="Revision">Update Build Revision (UBR).</param>
/// <param name="EditionId">Registry EditionID, e.g. "Professional".</param>
public sealed record JournalOs(int Build, int Revision, string EditionId);
