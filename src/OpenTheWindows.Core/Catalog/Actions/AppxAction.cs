namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Removes an Appx/MSIX package.</summary>
/// <param name="PackageFamilyName">Package family name, e.g. <c>Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe</c>.</param>
/// <param name="Mode">Current user only, or all users + deprovision.</param>
/// <param name="ReinstallHint">How a user gets it back (Store link, winget id, or "Add-AppxPackage -Register").</param>
public sealed record AppxAction(string PackageFamilyName, AppxRemovalMode Mode, string ReinstallHint) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "Appx";
}
