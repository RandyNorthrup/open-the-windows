namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Loads and unloads a user's registry hive file (<c>NTUSER.DAT</c>) so an
/// all-users apply can write to the hive of a user who is not logged on. A hive
/// that is already loaded (the user is signed in) is written directly and never
/// passed here. The caller loads before writing and unloads in a
/// <see langword="finally"/> so the hive is always released.
/// </summary>
public interface IUserHiveLoader
{
    /// <summary>
    /// Loads the hive file at <paramref name="hivePath"/> under
    /// <c>HKEY_USERS\<paramref name="mountKey"/></c>. Throws on failure.
    /// </summary>
    /// <param name="mountKey">The <c>HKEY_USERS</c> subkey name to mount at (the user's SID for a real user).</param>
    /// <param name="hivePath">Full path to the hive file, e.g. <c>C:\Users\jane\NTUSER.DAT</c>.</param>
    void Load(string mountKey, string hivePath);

    /// <summary>Unloads the hive mounted at <c>HKEY_USERS\<paramref name="mountKey"/></c>. Throws on failure.</summary>
    void Unload(string mountKey);
}
