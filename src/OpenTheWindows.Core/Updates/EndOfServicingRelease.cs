namespace OpenTheWindows.Core.Updates;

/// <summary>
/// One row of the end-of-servicing calendar: a Windows 11 release, the servicing
/// track it applies to, and the date that track stops receiving updates. Data
/// comes from the Microsoft release-health lifecycle pages (research 03 §4.2).
/// </summary>
/// <param name="Version">Marketing version, e.g. "24H2" (matches <c>OperatingSystemFacts.DisplayVersion</c>).</param>
/// <param name="Build">Major build number, e.g. 26100.</param>
/// <param name="Family">The servicing track this end-of-service date is for.</param>
/// <param name="EndOfService">The date servicing ends for this version and family.</param>
public sealed record EndOfServicingRelease(
    string Version,
    int Build,
    EndOfServicingEditionFamily Family,
    DateOnly EndOfService);
