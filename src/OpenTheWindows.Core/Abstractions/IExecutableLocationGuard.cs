namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Checks that an executable is safe to register for a privileged or other-user
/// launch (the SYSTEM scheduled task, the per-user Active Setup stub). If a standard
/// user could replace the file, drop a DLL beside it, or rename one of its folders,
/// registering it would hand that user SYSTEM (or every other user's session).
/// </summary>
public interface IExecutableLocationGuard
{
    /// <summary>
    /// Returns why <paramref name="executablePath"/> is unsafe to register, or
    /// <see langword="null"/> when the file and every folder above it are writable only
    /// by administrators.
    /// </summary>
    string? FindWeakness(string executablePath);
}
