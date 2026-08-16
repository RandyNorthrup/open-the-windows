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
