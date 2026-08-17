namespace OpenTheWindows.Core.Catalog;

/// <summary>Evidence that an entry was applied, verified and reverted on a specific build.</summary>
/// <param name="Build">OS build number, e.g. 26200.</param>
/// <param name="Edition">Edition family tested.</param>
/// <param name="Date">Date of the verification run.</param>
/// <param name="Evidence">Link or repository path to the certification record.</param>
public sealed record Verification(int Build, WindowsEdition Edition, DateOnly Date, string Evidence);
