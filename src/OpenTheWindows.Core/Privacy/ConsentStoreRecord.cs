namespace OpenTheWindows.Core.Privacy;

/// <summary>One app's usage record under the CapabilityAccessManager ConsentStore, as stored.</summary>
/// <param name="Capability">The capability key name.</param>
/// <param name="KeyName">The app's key name (a package family name, or a path with <c>#</c> for <c>\</c>).</param>
/// <param name="IsDesktopApp">True when the record is under <c>NonPackaged</c>.</param>
/// <param name="PerUser">True when read from the user's hive.</param>
/// <param name="LastUsedTimeStart">The <c>LastUsedTimeStart</c> FILETIME (0 when absent).</param>
/// <param name="LastUsedTimeStop">The <c>LastUsedTimeStop</c> FILETIME (0 when absent).</param>
public sealed record ConsentStoreRecord(
    string Capability,
    string KeyName,
    bool IsDesktopApp,
    bool PerUser,
    long LastUsedTimeStart,
    long LastUsedTimeStop);
