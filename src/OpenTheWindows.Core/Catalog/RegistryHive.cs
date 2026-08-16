namespace OpenTheWindows.Core.Catalog;

/// <summary>Registry root a registry action targets. User hives are resolved per scope at apply time.</summary>
public enum RegistryHive
{
    /// <summary>HKEY_LOCAL_MACHINE.</summary>
    LocalMachine = 0,

    /// <summary>The target user's hive (HKEY_USERS\&lt;SID&gt;), never the elevating account's HKCU.</summary>
    User = 1,
}
