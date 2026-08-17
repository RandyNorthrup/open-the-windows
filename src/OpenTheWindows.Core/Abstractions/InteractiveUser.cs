namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// The interactive session user (the person logged on at the console/RDP), whose
/// hive per-user tweaks target — never the elevating administrator account.
/// </summary>
/// <param name="Sid">The user's security identifier (S-1-5-21-...).</param>
/// <param name="UserName">Down-level user name (DOMAIN\\user).</param>
/// <param name="HiveLoaded">Whether the user's registry hive is currently loaded under HKEY_USERS.</param>
public sealed record InteractiveUser(string Sid, string UserName, bool HiveLoaded);
