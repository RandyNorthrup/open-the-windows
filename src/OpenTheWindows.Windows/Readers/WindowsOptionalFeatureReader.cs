using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a Windows optional feature's enabled state through WMI
/// (<c>Win32_OptionalFeature</c>). Read-only: it never enables or disables a
/// feature.
/// </summary>
/// <remarks>
/// The M2 spec suggested the DISM API, but opening a DISM session on the online
/// image requires administrator rights and the same API can also modify the
/// image. <c>Win32_OptionalFeature</c> is a strictly read-only source that the
/// scan can query without elevation (and is therefore testable on an
/// unelevated developer box), so it is used instead — recorded in PLAN §7.1.
/// <c>InstallState</c> maps 1 ⇒ enabled, 2 ⇒ disabled, and 3 (absent) / 4
/// (unknown) or no matching row ⇒ <see langword="null"/> (feature not present),
/// which the engine treats as not-applicable.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsOptionalFeatureReader : IOptionalFeatureReader
{
    private const int InstallStateEnabled = 1;
    private const int InstallStateDisabled = 2;

    /// <inheritdoc />
    public bool? IsEnabled(string featureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        // Feature names are catalogue-controlled and never contain quotes, but
        // double any single quote defensively so the WQL literal stays well-formed.
        string escaped = featureName.Replace("'", "''", StringComparison.Ordinal);
        var query = new ObjectQuery(string.Create(
            CultureInfo.InvariantCulture,
            $"SELECT InstallState FROM Win32_OptionalFeature WHERE Name = '{escaped}'"));

        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(@"\\.\root\CIMV2"), query);
        using ManagementObjectCollection results = searcher.Get();

        // Name is the key property, so the query returns at most one row.
        using ManagementBaseObject? item = results.Cast<ManagementBaseObject>().FirstOrDefault();
        if (item?["InstallState"] is not { } state)
        {
            return null;
        }

        return Convert.ToInt32(state, CultureInfo.InvariantCulture) switch
        {
            InstallStateEnabled => true,
            InstallStateDisabled => false,
            _ => null,
        };
    }
}
