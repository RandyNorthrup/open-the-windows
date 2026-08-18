namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Whether the running OS build differs from the build recorded in the most
/// recent journal, and the two builds for reporting. Used by
/// <c>remediate --if-build-changed</c> so the drift task re-applies a profile
/// only after a feature update (a build or UBR change), or when there is no
/// prior run to compare against.
/// </summary>
/// <param name="Changed"><see langword="true"/> when the build differs, or when there is no baseline to compare against.</param>
/// <param name="Current">The running build as "Build.UBR".</param>
/// <param name="Previous">The last journaled build as "Build.UBR", or <see langword="null"/> when no prior run exists.</param>
public sealed record BuildChangeStatus(bool Changed, string Current, string? Previous);
