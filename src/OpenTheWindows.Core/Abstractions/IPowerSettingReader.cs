namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads a power setting's AC/DC values on the active scheme. Never writes.</summary>
public interface IPowerSettingReader
{
    /// <summary>
    /// Returns the AC and DC value indices, or <see langword="null"/> when the
    /// subgroup/setting is not present on the active scheme.
    /// </summary>
    (uint Ac, uint Dc)? Read(Guid subgroup, Guid setting);
}
