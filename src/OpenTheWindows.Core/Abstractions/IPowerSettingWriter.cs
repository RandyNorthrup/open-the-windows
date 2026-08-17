namespace OpenTheWindows.Core.Abstractions;

/// <summary>Sets a power setting (AC and DC value) on the active power scheme.</summary>
public interface IPowerSettingWriter
{
    /// <summary>Sets the AC and DC value index for a setting and makes the change active.</summary>
    void SetValues(Guid subgroup, Guid setting, uint ac, uint dc);
}
