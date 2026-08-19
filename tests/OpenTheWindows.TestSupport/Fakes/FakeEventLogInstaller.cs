using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// In-memory <see cref="IEventLogInstaller"/> that records install/uninstall calls and
/// performs no OS action. <see cref="Registered"/> is the pretend registration state;
/// <see cref="ThrowOnInstall"/> and <see cref="ThrowOnUninstall"/> simulate the elevation
/// failure the real APIs raise, so the command's error path can be exercised.
/// </summary>
public sealed class FakeEventLogInstaller : IEventLogInstaller
{
    /// <summary>Whether the source is currently (pretend) registered.</summary>
    public bool Registered { get; set; }

    /// <summary>Number of times <see cref="Install"/> was called.</summary>
    public int InstallCalls { get; private set; }

    /// <summary>Number of times <see cref="Uninstall"/> was called.</summary>
    public int UninstallCalls { get; private set; }

    /// <summary>When set, <see cref="Install"/> throws it (an elevation/access failure).</summary>
    public Exception? ThrowOnInstall { get; set; }

    /// <summary>When set, <see cref="Uninstall"/> throws it (an elevation/access failure).</summary>
    public Exception? ThrowOnUninstall { get; set; }

    /// <inheritdoc />
    public string LogName => "OpenTheWindows";

    /// <inheritdoc />
    public string SourceName => "OTW";

    /// <inheritdoc />
    public bool SourceExists() => Registered;

    /// <inheritdoc />
    public bool Install()
    {
        InstallCalls++;
        if (ThrowOnInstall is not null)
        {
            throw ThrowOnInstall;
        }

        if (Registered)
        {
            return false;
        }

        Registered = true;
        return true;
    }

    /// <inheritdoc />
    public bool Uninstall()
    {
        UninstallCalls++;
        if (ThrowOnUninstall is not null)
        {
            throw ThrowOnUninstall;
        }

        if (!Registered)
        {
            return false;
        }

        Registered = false;
        return true;
    }
}
