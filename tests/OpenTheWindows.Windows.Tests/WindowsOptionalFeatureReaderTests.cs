using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real optional-feature state through WMI (read-only, no elevation
/// needed). Uses features whose default state is stable across Windows 11 boxes.
/// </summary>
public sealed class WindowsOptionalFeatureReaderTests
{
    [Fact]
    public void Enabled_feature_reads_true()
    {
        var reader = new WindowsOptionalFeatureReader();

        // Print-to-PDF ships enabled on Windows 11.
        bool? enabled = reader.IsEnabled("Printing-PrintToPDFServices-Features");

        Assert.True(enabled);
    }

    [Fact]
    public void Disabled_feature_reads_false()
    {
        var reader = new WindowsOptionalFeatureReader();

        // The Telnet client ships present-but-disabled on Windows 11.
        bool? enabled = reader.IsEnabled("TelnetClient");

        Assert.False(enabled);
    }

    [Fact]
    public void Unknown_feature_reads_null()
    {
        var reader = new WindowsOptionalFeatureReader();

        bool? enabled = reader.IsEnabled("OtwNoSuchOptionalFeature");

        Assert.Null(enabled);
    }
}
