using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Enumerates the machine's autostarts: services, scheduled tasks, Run/RunOnce values and Startup-folder items (read-only).</summary>
public interface IPersistenceSource
{
    /// <summary>Every autostart found now, including the signed-in user's when <paramref name="userSid"/> is given.</summary>
    IReadOnlyList<PersistenceItem> Capture(string? userSid);
}
