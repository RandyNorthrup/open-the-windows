namespace OpenTheWindows.Core.Model;

/// <summary>
/// What must happen after a tweak is applied for it to take full effect. The
/// engine aggregates the maximum over an apply run and reports it once.
/// Ordered so that <c>Max()</c> yields the most disruptive requirement.
/// </summary>
public enum RestartRequirement
{
    /// <summary>Takes effect immediately.</summary>
    None = 0,

    /// <summary>Explorer.exe must be restarted (shell/taskbar settings).</summary>
    ExplorerRestart = 1,

    /// <summary>The user must sign out and back in.</summary>
    SignOut = 2,

    /// <summary>The machine must reboot.</summary>
    Reboot = 3,
}
