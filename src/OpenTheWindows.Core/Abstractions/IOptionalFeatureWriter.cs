namespace OpenTheWindows.Core.Abstractions;

/// <summary>Enables or disables a Windows optional feature (DISM feature name).</summary>
public interface IOptionalFeatureWriter
{
    /// <summary>Sets the feature's enabled state. May report a restart requirement to the caller separately.</summary>
    void SetEnabled(string name, bool enabled);
}
