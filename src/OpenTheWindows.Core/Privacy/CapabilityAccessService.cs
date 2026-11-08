using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Privacy;

/// <summary>
/// The read-only "which apps used my camera, microphone or location, and when" view,
/// from the last-used times Windows records per app in the CapabilityAccessManager
/// ConsentStore. Nothing is changed.
/// </summary>
public sealed class CapabilityAccessService
{
    /// <summary>The camera capability.</summary>
    public const string Webcam = "webcam";

    /// <summary>The microphone capability.</summary>
    public const string Microphone = "microphone";

    /// <summary>The location capability.</summary>
    public const string Location = "location";

    /// <summary>The capabilities shown unless every one is asked for.</summary>
    public static IReadOnlyList<string> DefaultCapabilities { get; } = [Webcam, Microphone, Location];

    private const char DesktopPathSeparator = '#';

    private readonly IConsentStoreSource _source;
    private readonly IInteractiveUserResolver _users;

    /// <summary>Creates the service over the ConsentStore source and the interactive-user resolver.</summary>
    public CapabilityAccessService(IConsentStoreSource source, IInteractiveUserResolver users)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(users);
        _source = source;
        _users = users;
    }

    /// <summary>
    /// The apps that have used <paramref name="capabilities"/> (every capability when
    /// <see langword="null"/>), most recent first. Records with no usage time are left out.
    /// </summary>
    public IReadOnlyList<CapabilityUse> Recent(IReadOnlyCollection<string>? capabilities)
    {
        string? sid = _users.Resolve()?.Sid;
        return [.. _source.List(sid)
            .Where(r => r.LastUsedTimeStart > 0
                && (capabilities is null || capabilities.Contains(r.Capability, StringComparer.OrdinalIgnoreCase)))
            .Select(ToUse)
            .OrderByDescending(u => u.InUse)
            .ThenByDescending(u => u.LastStarted)];
    }

    /// <summary>Converts a stored FILETIME to a time, or <see langword="null"/> when it is 0 or out of range.</summary>
    public static DateTimeOffset? FromFileTime(long fileTime)
    {
        if (fileTime <= 0 || fileTime > DateTimeOffset.MaxValue.ToFileTime())
        {
            return null;
        }

        return DateTimeOffset.FromFileTime(fileTime).ToUniversalTime();
    }

    private static CapabilityUse ToUse(ConsentStoreRecord record) => new(
        record.Capability,
        record.IsDesktopApp ? record.KeyName.Replace(DesktopPathSeparator, '\\') : record.KeyName,
        record.IsDesktopApp,
        record.PerUser,
        FromFileTime(record.LastUsedTimeStart),
        FromFileTime(record.LastUsedTimeStop));
}
