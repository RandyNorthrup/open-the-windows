namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Whether a registry value is a Group Policy setting (under a <c>Policies</c>
/// key; written through the Local GPO and mirrored) or a plain preference.
/// </summary>
public enum RegistryValueKind
{
    /// <summary>User/machine preference; written directly.</summary>
    Preference = 0,

    /// <summary>Policy value; greys out UI, wins over preferences, must survive gpupdate.</summary>
    Policy = 1,
}
