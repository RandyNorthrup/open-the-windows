namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Registers and removes the Open the Windows Windows Event Log source, so audit
/// events have somewhere to be written before the first elevated operation runs
/// (the installer creates the source at install time). Implementations are
/// idempotent: installing an existing source, or uninstalling an absent one, is a
/// no-op that reports what it found. Registration needs elevation; when it is not
/// permitted the methods surface the platform exception rather than pretending to
/// succeed.
/// </summary>
public interface IEventLogInstaller
{
    /// <summary>The custom Event Log the source writes into (for example <c>OpenTheWindows</c>).</summary>
    string LogName { get; }

    /// <summary>The Event Log source audit entries are written under (for example <c>OTW</c>).</summary>
    string SourceName { get; }

    /// <summary>Reports whether <see cref="SourceName"/> is already registered.</summary>
    bool SourceExists();

    /// <summary>
    /// Ensures the source exists. Returns <see langword="true"/> when this call created it and
    /// <see langword="false"/> when it was already present. Throws when the caller lacks the
    /// elevation the Event Log APIs require.
    /// </summary>
    bool Install();

    /// <summary>
    /// Removes the source and the custom log (both are owned by Open the Windows). Returns
    /// <see langword="true"/> when something was removed and <see langword="false"/> when there was
    /// nothing to remove. Throws when the caller lacks the required elevation.
    /// </summary>
    bool Uninstall();
}
