namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// A real (non-system) user profile on the machine, taken from the ProfileList,
/// with whether its per-user hive (<c>HKEY_USERS\&lt;Sid&gt;</c>) is currently
/// loaded. These are the targets of an all-users profile application: a loaded
/// hive is written directly, an unloaded one must be loaded from its NTUSER.DAT
/// first.
/// </summary>
/// <param name="Sid">The account SID, e.g. "S-1-5-21-...".</param>
/// <param name="ProfilePath">The profile directory (registry <c>ProfileImagePath</c>), e.g. "C:\Users\jane".</param>
/// <param name="HiveLoaded"><see langword="true"/> when <c>HKEY_USERS\&lt;Sid&gt;</c> is present (the user is logged on, or the hive is otherwise loaded).</param>
public sealed record UserProfile(string Sid, string ProfilePath, bool HiveLoaded);
