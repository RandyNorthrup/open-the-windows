using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real Appx package state (read-only, no elevation needed). Uses the
/// Microsoft Store package, present for the current user on every Windows 11 box.
/// The all-users / provisioned enumeration needs elevation, so unelevated these
/// tests exercise the documented degraded path (see <see cref="WindowsAppxReader"/>).
/// </summary>
public sealed class WindowsAppxReaderTests
{
    // Microsoft Store: installed for the interactive user on every Windows 11 box.
    private const string StoreFamilyName = "Microsoft.WindowsStore_8wekyb3d8bbwe";

    [Fact]
    public void Reports_a_package_installed_for_the_current_user()
    {
        var reader = new WindowsAppxReader();

        AppxSnapshot snapshot = reader.Read(StoreFamilyName, userSid: null);

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
