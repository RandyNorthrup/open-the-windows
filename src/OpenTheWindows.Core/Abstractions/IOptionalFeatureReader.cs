namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads a Windows optional feature's enabled state. Never writes.</summary>
public interface IOptionalFeatureReader
{
    /// <summary>
    /// Returns whether the feature is enabled, or <see langword="null"/> when the
    /// feature is not present on this edition/build.
    /// </summary>
    bool? IsEnabled(string featureName);
}
