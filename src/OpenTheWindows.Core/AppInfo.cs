using System.Reflection;

namespace OpenTheWindows.Core;

/// <summary>
/// Product identity shared by every front end. Title only; the product has
/// no tagline (product decision, see PLAN.md "Branding").
/// </summary>
public static class AppInfo
{
    /// <summary>Product title as shown in window chrome and CLI banners.</summary>
    public const string Title = "Open the Windows";

    /// <summary>Short executable name of the CLI.</summary>
    public const string CliName = "otw";

    /// <summary>Canonical project home, used as the SARIF tool <c>informationUri</c>.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S1075:URIs should not be hardcoded",
        Justification = "Immutable product-identity constant (the project homepage) used as the SARIF tool " +
            "informationUri; it is not environment-specific configuration. Recorded in PLAN.md §7.3.")]
    public const string HomepageUri = "https://github.com/RandyNorthrup/open-the-windows";

    /// <summary>
    /// Informational version of the running Core assembly (from
    /// <see cref="AssemblyInformationalVersionAttribute"/>), falling back to the
    /// assembly version when the attribute is absent.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(AppInfo).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the "+<commit-hash>" suffix SourceLink appends; keep it in --version --verbose.
            int plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
