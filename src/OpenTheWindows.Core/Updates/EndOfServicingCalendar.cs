using System.Reflection;
using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Answers "how long until this Windows 11 release stops getting updates?" from
/// the embedded <c>catalog/updates/eos.json</c> snapshot (research 03 §4.2). Used
/// by <c>otw updates status</c> to warn before pinning a device to a version that
/// is close to end-of-servicing.
/// </summary>
public sealed class EndOfServicingCalendar
{
    /// <summary>Warn when a release is within this many days of end-of-servicing (research 03 §7.2).</summary>
    public const int WarningWindowDays = 90;

    /// <summary>Logical name of the embedded calendar data file.</summary>
    public const string ResourceName = "data/eos.json";

    private readonly Dictionary<(string Version, EndOfServicingEditionFamily Family), EndOfServicingRelease> _byKey;

    /// <summary>Creates the calendar from a parsed document.</summary>
    public EndOfServicingCalendar(EndOfServicingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Source = document.Source;
        AsOf = document.AsOf;
        _byKey = document.Releases.ToDictionary(r => (r.Version, r.Family));
    }

    /// <summary>Where the dates came from.</summary>
    public string Source { get; }

    /// <summary>When the snapshot was taken.</summary>
    public DateOnly AsOf { get; }

    /// <summary>Loads the calendar embedded in this assembly.</summary>
    public static EndOfServicingCalendar LoadBuiltIn()
    {
        Assembly assembly = typeof(EndOfServicingCalendar).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        EndOfServicingDocument document = JsonSerializer.Deserialize(stream, EndOfServicingJsonContext.Default.EndOfServicingDocument)
            ?? throw new InvalidOperationException("The end-of-servicing calendar deserialised to null.");
        return new EndOfServicingCalendar(document);
    }

    /// <summary>The servicing track a Windows edition belongs to.</summary>
    public static EndOfServicingEditionFamily FamilyFor(WindowsEdition edition)
        => edition is WindowsEdition.Enterprise or WindowsEdition.Education
            ? EndOfServicingEditionFamily.Enterprise
            : EndOfServicingEditionFamily.Consumer;

    /// <summary>Finds the calendar row for a version and family, or <see langword="null"/> when it is unknown.</summary>
    public EndOfServicingRelease? Find(string version, EndOfServicingEditionFamily family)
        => _byKey.GetValueOrDefault((version, family));

    /// <summary>
    /// Days from <paramref name="asOf"/> until the version's servicing ends
    /// (negative when already past), or <see langword="null"/> when the version is
    /// not in the calendar.
    /// </summary>
    public int? DaysUntilEndOfService(string version, EndOfServicingEditionFamily family, DateTimeOffset asOf)
    {
        if (Find(version, family) is not { } release)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        return release.EndOfService.DayNumber - today.DayNumber;
    }

    /// <summary>
    /// Whether a version is within the 90-day warning window or already past
    /// end-of-servicing. <see langword="false"/> for unknown versions.
    /// </summary>
    public bool IsWithinWarningWindow(string version, EndOfServicingEditionFamily family, DateTimeOffset asOf)
        => DaysUntilEndOfService(version, family, asOf) is { } days && days <= WarningWindowDays;
}
