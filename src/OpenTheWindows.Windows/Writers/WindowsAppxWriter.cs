using System.Globalization;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Removes and reprovisions Appx/MSIX packages through the WinRT
/// <see cref="PackageManager"/>. Per-user removal cannot be undone by this
/// product (the package bits are gone); revert of an all-users deprovision
/// reprovisions the package so new users receive it again.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class WindowsAppxWriter : IAppxWriter
{
    /// <inheritdoc />
    public void Remove(string packageFamilyName, string? userSid, AppxRemovalMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFamilyName);

        var manager = new PackageManager();
        if (mode == AppxRemovalMode.CurrentUser)
        {
            foreach (Package package in Matching(manager.FindPackagesForUser(userSid ?? string.Empty), packageFamilyName))
            {
                Await(manager.RemovePackageAsync(package.Id.FullName, RemovalOptions.None), ignoreMissing: true);
            }

            return;
        }

        foreach (Package package in Matching(manager.FindPackages(), packageFamilyName))
        {
            Await(manager.RemovePackageAsync(package.Id.FullName, RemovalOptions.RemoveForAllUsers), ignoreMissing: true);
        }

        // Deprovisioning a package that was never provisioned reports "not found"; that already
        // meets the intent (it cannot return for new users), so the absence is treated as success.
        Await(manager.DeprovisionPackageForAllUsersAsync(packageFamilyName), ignoreMissing: true);
    }

    /// <inheritdoc />
    public void Reprovision(string packageFamilyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFamilyName);
        Await(new PackageManager().ProvisionPackageForAllUsersAsync(packageFamilyName), ignoreMissing: false);
    }

    private static IEnumerable<Package> Matching(IEnumerable<Package> packages, string packageFamilyName)
        => packages.Where(p => string.Equals(p.Id.FamilyName, packageFamilyName, StringComparison.OrdinalIgnoreCase));

    // The IAppxWriter contract is synchronous and the host is a console/service with no
    // synchronization context, so awaiting the WinRT deployment operation to completion here
    // cannot deadlock. VSTHRD002 is suppressed for this single bridging point.
#pragma warning disable VSTHRD002
    private static void Await(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> operation, bool ignoreMissing)
    {
        DeploymentResult result = operation.AsTask().GetAwaiter().GetResult();
        if (result.ExtendedErrorCode is null)
        {
            return;
        }

        // ERROR_NOT_FOUND (0x80070490): the package or its provisioning is already absent — for a
        // removal or deprovision that is the desired state, so it is not a failure.
        if (ignoreMissing && result.ExtendedErrorCode.HResult == unchecked((int)0x80070490))
        {
            return;
        }

        throw new InvalidOperationException(
            string.Create(CultureInfo.InvariantCulture, $"Appx deployment failed: {result.ErrorText}"),
            result.ExtendedErrorCode);
    }
#pragma warning restore VSTHRD002
}
