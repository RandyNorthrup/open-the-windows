namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Snapshot of OS identity used for support checks and catalogue applicability.
/// </summary>
/// <param name="ProductName">e.g. "Windows 11 Pro" (registry <c>ProductName</c>, corrected for Windows 11).</param>
/// <param name="EditionId">Registry <c>EditionID</c>, e.g. "Professional", "Enterprise", "Core" (Home).</param>
/// <param name="DisplayVersion">Marketing version, e.g. "24H2".</param>
/// <param name="Build">Major build number, e.g. 26100.</param>
/// <param name="Revision">Update Build Revision (UBR), e.g. 4351.</param>
/// <param name="Architecture">Process architecture of the OS, e.g. "x64" or "arm64".</param>
/// <param name="InstallationType">Registry <c>InstallationType</c>: "Client" or "Server".</param>
public sealed record OperatingSystemFacts(
    string ProductName,
    string EditionId,
    string DisplayVersion,
    int Build,
    int Revision,
    string Architecture,
    string InstallationType)
{
    /// <summary>First Windows 11 build (21H2).</summary>
    public const int FirstWindows11Build = 22000;

    /// <summary><see langword="true"/> for any Windows 11 client build.</summary>
    public bool IsWindows11 => Build >= FirstWindows11Build && !IsServer;

    /// <summary><see langword="true"/> for Windows Server SKUs, which this product does not support.</summary>
    public bool IsServer => string.Equals(InstallationType, "Server", StringComparison.OrdinalIgnoreCase);

    /// <summary>"26100.4351" style full build string.</summary>
    public string FullBuild => $"{Build}.{Revision}";
}
