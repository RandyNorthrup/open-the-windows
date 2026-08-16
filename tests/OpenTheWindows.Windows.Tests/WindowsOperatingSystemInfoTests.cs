using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads the real registry (read-only, no elevation needed). Runs on any
/// Windows machine; the assertions are invariant across supported builds.
/// </summary>
public sealed class WindowsOperatingSystemInfoTests
{
    [Fact]
    public void GetFacts_reads_a_plausible_windows_identity()
    {
        var sut = new WindowsOperatingSystemInfo();

        OperatingSystemFacts facts = sut.GetFacts();

        Assert.False(string.IsNullOrWhiteSpace(facts.ProductName));
        Assert.False(string.IsNullOrWhiteSpace(facts.EditionId));
        Assert.False(string.IsNullOrWhiteSpace(facts.DisplayVersion));
        Assert.InRange(facts.Build, 10240, 99999);
        Assert.InRange(facts.Revision, 0, 999999);
        Assert.Contains(facts.InstallationType, ["Client", "Server"], StringComparer.Ordinal);
        Assert.Contains(facts.Architecture, ["x64", "arm64", "x86"], StringComparer.Ordinal);
        Assert.Equal($"{facts.Build}.{facts.Revision}", facts.FullBuild);
    }

    [Fact]
    public void GetFacts_corrects_product_name_on_windows_11()
    {
        OperatingSystemFacts facts = new WindowsOperatingSystemInfo().GetFacts();

        if (facts.Build >= OperatingSystemFacts.FirstWindows11Build && !facts.IsServer)
        {
            Assert.StartsWith("Windows 11", facts.ProductName, StringComparison.Ordinal);
            Assert.True(facts.IsWindows11);
        }
        else
        {
            Assert.False(facts.IsWindows11);
        }
    }
}
