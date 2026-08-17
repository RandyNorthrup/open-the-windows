namespace OpenTheWindows.Cli;

/// <summary>One entry's outcome in <see cref="ApplyJsonReport"/>.</summary>
/// <param name="Id">The catalogue entry id.</param>
/// <param name="Result">The entry's outcome.</param>
/// <param name="Risk">The entry's risk tier.</param>
/// <param name="Requires">The entry's restart requirement.</param>
/// <param name="Note">A human-readable explanation, when relevant.</param>
internal sealed record ApplyJsonEntry(string Id, string Result, string Risk, string Requires, string? Note);
