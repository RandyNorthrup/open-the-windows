using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Removes and reprovisions Appx/MSIX packages. Per-user removal is not
/// reversible by this product (the package bits are gone); revert of a
/// deprovision reprovisions the package so new users receive it again, and the
/// catalogue entry's reinstall hint tells the current user how to reinstall.
/// </summary>
public interface IAppxWriter
{
    /// <summary>Removes the package for the target user, or for all users and deprovisions it.</summary>
    void Remove(string packageFamilyName, string? userSid, AppxRemovalMode mode);

    /// <summary>Reprovisions a previously deprovisioned package (revert of a deprovision).</summary>
    void Reprovision(string packageFamilyName);
}
