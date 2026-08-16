using System.Runtime.Versioning;
using System.Security.Principal;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Elevation facts from the process token. Under UAC a non-elevated
/// administrator's token has the Administrators group marked deny-only, so
/// <see cref="WindowsPrincipal.IsInRole(WindowsBuiltInRole)"/> is
/// <see langword="false"/> until the process is actually elevated — which is
/// exactly the semantics the engine needs.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsElevationContext : IElevationContext
{
    public WindowsElevationContext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        IsElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
        ProcessUserName = identity.Name;
    }

    /// <inheritdoc />
    public bool IsElevated { get; }

    /// <inheritdoc />
    public string ProcessUserName { get; }
}
