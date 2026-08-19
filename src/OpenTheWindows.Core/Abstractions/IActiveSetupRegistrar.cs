namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Registers (and removes) the product's Active Setup components — the per-user
/// logon fallback for an all-users apply. Writing under
/// <c>HKLM\SOFTWARE\Microsoft\Active Setup</c> is a machine change and so is only
/// reached from the already-elevated all-users apply path; the stub it installs
/// runs non-elevated in each user's own session and touches only that user's hive.
/// </summary>
public interface IActiveSetupRegistrar
{
    /// <summary>
    /// Writes <paramref name="component"/> under Active Setup, bumping its
    /// <c>Version</c> so Windows re-runs the stub at every user's next sign-in.
    /// Does nothing while Windows setup / OOBE is in progress.
    /// </summary>
    ActiveSetupResult Register(ActiveSetupComponent component);

    /// <summary>
    /// Removes every Active Setup component this product registered (those whose key
    /// name starts with the <c>{OTW-</c> prefix), so the per-user logon fallback
    /// stops firing. Returns the number of components removed.
    /// </summary>
    int RemoveAll();
}
