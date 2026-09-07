namespace OpenTheWindows.Core.Releases;

/// <summary>
/// Opt-in check against GitHub Releases. Implementations must not run unless the
/// user invoked them; the product has no background network.
/// </summary>
public interface IAppReleaseChecker
{
    /// <summary>Queries the latest published release and compares it to the running version.</summary>
    Task<AppReleaseCheckResult> CheckAsync(CancellationToken cancellationToken);
}
