namespace OpenTheWindows.Core.Releases;

/// <summary>Outcome of one opt-in query against GitHub Releases.</summary>
/// <param name="Succeeded">Whether the HTTP request completed and the payload parsed.</param>
/// <param name="UpdateAvailable">True only when a newer non-draft, non-prerelease tag exists.</param>
/// <param name="CurrentVersion">The running product version.</param>
/// <param name="LatestVersion">The latest release tag, when known.</param>
/// <param name="ReleaseUri">The GitHub release HTML URI, when known.</param>
/// <param name="Message">Human-readable outcome; never contains secrets.</param>
public sealed record AppReleaseCheckResult(
    bool Succeeded,
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    Uri? ReleaseUri,
    string Message);
