using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Removes and reprovisions Appx/MSIX packages. Removal is not reversible by this
/// product: the catalogue entry's reinstall hint says how to get the app back.
/// Reprovisioning after a deprovision only works while Windows still has the
/// package staged; on Windows 11 24H2/25H2 an all-users removal deletes the
/// package files, so the attempt fails and the revert reports the entry as not
/// reversible.
/// </summary>
public interface IAppxWriter
{
    /// <summary>Removes the package for the target user, or for all users and deprovisions it.</summary>
    void Remove(string packageFamilyName, string? userSid, AppxRemovalMode mode);

    /// <summary>Reprovisions a previously deprovisioned package that Windows still has staged.</summary>
    void Reprovision(string packageFamilyName);
}
