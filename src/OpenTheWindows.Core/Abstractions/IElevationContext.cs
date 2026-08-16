namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Who the process is running as and whether it holds an elevated token.
/// The engine refuses machine-scope writes when <see cref="IsElevated"/> is
/// <see langword="false"/>.
/// </summary>
public interface IElevationContext
{
    /// <summary><see langword="true"/> when the process token is a member of the local Administrators group with elevation.</summary>
    bool IsElevated { get; }

    /// <summary>DOMAIN\user of the process token (the elevating account, not necessarily the interactive user).</summary>
    string ProcessUserName { get; }
}
