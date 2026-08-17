using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads whether an Appx/MSIX package is present, for the interactive user and
/// for all users, plus its provisioned and deprovisioned state. Read-only: it
/// never removes, stages or provisions a package.
/// </summary>
/// <remarks>
/// <para>
/// Per-user presence (<see cref="AppxSnapshot.InstalledForUser"/>) comes from
/// <see cref="PackageManager.FindPackagesForUser(string)"/>, which succeeds
/// without elevation. The all-users enumeration
/// (<see cref="PackageManager.FindPackages()"/>) and the provisioned-package
/// list (<see cref="PackageManager.FindProvisionedPackages"/>) require
/// administrator rights; when they are denied, the reader degrades to the
/// user-scoped view — <see cref="AppxSnapshot.InstalledForAnyUser"/> becomes a
/// lower bound (true only if visible for the interactive user) and
/// <see cref="AppxSnapshot.Provisioned"/> reports <see langword="false"/>. This
/// only affects all-users removals, which themselves require elevation, so the
/// scan is re-run elevated before any such apply. The degradation is documented,
/// never a silent success.
/// </para>
/// <para>
/// Deprovisioning is recorded as a subkey under
/// <c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned</c>,
/// which is readable without elevation.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class WindowsAppxReader : IAppxReader
{
    private const string DeprovisionedKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Deprovisioned";

    /// <inheritdoc />
    public AppxSnapshot Read(string packageFamilyName, string? userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFamilyName);

        var manager = new PackageManager();

        // Empty string means the current user for FindPackagesForUser.
        bool installedForUser = ContainsFamily(
            manager.FindPackagesForUser(userSid ?? string.Empty), packageFamilyName);

        bool installedForAnyUser;
        bool provisioned;
        try
        {
            installedForAnyUser = ContainsFamily(manager.FindPackages(), packageFamilyName);
            provisioned = ContainsFamily(manager.FindProvisionedPackages(), packageFamilyName);
        }
        catch (UnauthorizedAccessException)
        {
            // All-users / provisioned enumeration needs elevation; degrade to the
            // user-scoped lower bound (see the class remarks). Not a silent success.
            installedForAnyUser = installedForUser;
            provisioned = false;
        }

        bool deprovisioned = IsDeprovisioned(packageFamilyName);
        return new AppxSnapshot(installedForUser, installedForAnyUser, provisioned, deprovisioned);
    }

    private static bool ContainsFamily(IEnumerable<Package> packages, string packageFamilyName)
    {
        return packages.Any(package => string.Equals(
            package.Id.FamilyName, packageFamilyName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDeprovisioned(string packageFamilyName)
    {
        using RegistryKey? deprovisioned = Registry.LocalMachine.OpenSubKey(DeprovisionedKeyPath, writable: false);
        if (deprovisioned is null)
        {
            return false;
        }

        using RegistryKey? entry = deprovisioned.OpenSubKey(packageFamilyName, writable: false);
        return entry is not null;
    }
}
