namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Restarts <c>explorer.exe</c> in a specific user's interactive session so that
/// shell/taskbar settings take effect without a sign-out. Restarting the
/// elevating account's Explorer would be the wrong session; the target SID is
/// the interactive user.
/// </summary>
public interface IExplorerRestarter
{
    /// <summary>Restarts Explorer for the session owned by <paramref name="userSid"/>.</summary>
    void RestartForSession(string userSid);
}
