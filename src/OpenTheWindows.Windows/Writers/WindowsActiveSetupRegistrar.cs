using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Registers the product's Active Setup components under
/// <c>HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components</c>. Windows runs a
/// component's <c>StubPath</c> in each user's own session at their next sign-in
/// whenever the machine-wide <c>Version</c> is newer than that user's copy — the
/// standard per-user provisioning mechanism, here used so an all-users apply reaches
/// users who were logged off (or created later). Writing here is a machine change and
/// is only reached from the already-elevated all-users apply; the stub it installs
/// runs unelevated and touches only the signing-in user's hive.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsActiveSetupRegistrar : IActiveSetupRegistrar
{
    /// <inheritdoc />
    public ActiveSetupResult Register(ActiveSetupComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        // Never provision a per-user logon stub while an image is being generalised: an
        // OOBE-time write would bake the component into every account cloned from the image.
        if (IsOobeInProgress())
        {
            return new ActiveSetupResult(ActiveSetupOutcome.SkippedDuringOobe, null, component.ComponentKey);
        }

        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey key = hklm.CreateSubKey(
            string.Create(CultureInfo.InvariantCulture, $@"{ActiveSetupFallback.InstalledComponentsKey}\{component.ComponentKey}"),
            writable: true);

        string version = NextVersion(key).ToString(CultureInfo.InvariantCulture);
        key.SetValue(null, component.DisplayName, RegistryValueKind.String);   // (Default) — the component name
        key.SetValue("StubPath", component.StubPath, RegistryValueKind.String);
        key.SetValue("Version", version, RegistryValueKind.String);           // Active Setup versions are REG_SZ
        key.SetValue("IsInstalled", 1, RegistryValueKind.DWord);
        return new ActiveSetupResult(ActiveSetupOutcome.Registered, version, component.ComponentKey);
    }

    /// <inheritdoc />
    public int RemoveAll()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? parent = hklm.OpenSubKey(ActiveSetupFallback.InstalledComponentsKey, writable: true);
        if (parent is null)
        {
            return 0;
        }

        int removed = 0;
        foreach (string name in parent.GetSubKeyNames()
            .Where(n => n.StartsWith(ActiveSetupFallback.ComponentKeyPrefix, StringComparison.Ordinal)))
        {
            parent.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// The next <c>Version</c> value: the existing integer plus one, or 1 when the
    /// component is new or its version is missing/unparseable. Bumping guarantees
    /// Windows re-runs the stub at each user's next sign-in.
    /// </summary>
    private static int NextVersion(RegistryKey key)
    {
        if (key.GetValue("Version") is string existing
            && int.TryParse(existing, NumberStyles.Integer, CultureInfo.InvariantCulture, out int current)
            && current > 0)
        {
            return current + 1;
        }

        return 1;
    }

    private static bool IsOobeInProgress()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\Setup", writable: false);
        return key?.GetValue("OOBEInProgress") is int flag && flag != 0;
    }
}
