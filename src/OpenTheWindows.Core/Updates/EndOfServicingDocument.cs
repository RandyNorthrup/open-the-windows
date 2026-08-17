namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The deserialised <c>catalog/updates/eos.json</c> document: the source date and
/// the list of end-of-servicing rows.
/// </summary>
/// <param name="Source">Where the dates came from (a Microsoft lifecycle URL).</param>
/// <param name="AsOf">The date the calendar snapshot was taken (YYYY-MM-DD).</param>
/// <param name="Releases">One row per version and servicing track.</param>
public sealed record EndOfServicingDocument(
    string Source,
    DateOnly AsOf,
    IReadOnlyList<EndOfServicingRelease> Releases);
