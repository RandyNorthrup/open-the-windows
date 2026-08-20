using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Readers;
using Windows.Management.Deployment;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real Appx package state (read-only, no elevation needed). The positive
/// case discovers a package actually installed for the current user rather than
/// assuming a specific one: the Microsoft Store ships on Windows 11 clients but
/// not on the Windows Server image CI runs on. The all-users / provisioned
/// enumeration needs elevation, so unelevated these tests exercise the
/// documented degraded path (see <see cref="WindowsAppxReader"/>).
/// </summary>
public sealed class WindowsAppxReaderTests
{
    [Fact]
    public void Reports_a_package_installed_for_the_current_user()
    {
        // Empty string selects the current user. Pick whatever is installed; on a
        // host with no per-user packages (e.g. the Windows Server CI runner) there
        // is nothing to assert the reader against, so skip rather than assume the
        // Store is present.
        string? family = new PackageManager()
            .FindPackagesForUser(string.Empty)
            .Select(package => package.Id.FamilyName)
            .FirstOrDefault();
        Assert.SkipWhen(family is null, "No per-user Appx package on this host (e.g. the Windows Server CI runner).");

        var reader = new WindowsAppxReader();

        AppxSnapshot snapshot = reader.Read(family, userSid: null);

        Assert.True(snapshot.InstalledForUser);
    }

    [Fact]
    public void Absent_package_is_not_installed_or_deprovisioned()
    {
        var reader = new WindowsAppxReader();

        AppxSnapshot snapshot = reader.Read("OpenTheWindows.NoSuchPackage_0000000000000", userSid: null);

        Assert.False(snapshot.InstalledForUser);
        Assert.False(snapshot.InstalledForAnyUser);
        Assert.False(snapshot.Provisioned);
        Assert.False(snapshot.Deprovisioned);
    }
}
