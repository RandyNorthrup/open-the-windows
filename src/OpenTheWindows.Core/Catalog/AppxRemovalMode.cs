namespace OpenTheWindows.Core.Catalog;

/// <summary>How an Appx package is removed.</summary>
public enum AppxRemovalMode
{
    /// <summary>Remove for the target user only.</summary>
    CurrentUser = 0,

    /// <summary>Remove for all users and deprovision so new users do not get it (sets the Deprovisioned marker).</summary>
    AllUsersAndDeprovision = 1,
}
