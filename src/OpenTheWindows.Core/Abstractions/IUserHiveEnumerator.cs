namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Enumerates the real user profiles on the machine (from the ProfileList),
/// skipping the system service accounts and the default profile. All-users
/// profile application targets each of these hives.
/// </summary>
public interface IUserHiveEnumerator
{
    /// <summary>The machine's real user profiles, in a stable order (by SID).</summary>
    IReadOnlyList<UserProfile> Enumerate();
}
