using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads what Windows starts at sign-in: Run-key values and Startup-folder items (read-only).</summary>
public interface IStartupSource
{
    /// <summary>The machine-wide entries and, when <paramref name="userSid"/> is given, that user's.</summary>
    IReadOnlyList<StartupEntry> List(string? userSid);
}
