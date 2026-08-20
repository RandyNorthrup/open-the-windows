namespace OpenTheWindows.Core.Catalog;

/// <summary>Records that an entry was applied, verified and reverted on a specific Windows build.</summary>
/// <param name="Build">OS build number, e.g. 26200.</param>
/// <param name="Edition">Edition family tested.</param>
/// <param name="Date">Date of the verification run.</param>
public sealed record Verification(int Build, WindowsEdition Edition, DateOnly Date);
