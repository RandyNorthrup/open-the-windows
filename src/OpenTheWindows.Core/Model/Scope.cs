namespace OpenTheWindows.Core.Model;

/// <summary>
/// Where a tweak's state lives. Determines which registry root the engine
/// targets and whether "all users" fan-out is possible.
/// </summary>
public enum Scope
{
    /// <summary>Machine-wide (HKLM, services, tasks, provisioned packages, machine policy).</summary>
    Machine = 0,

    /// <summary>
    /// The interactive user's profile (HKU\&lt;SID&gt; of the logged-on session user,
    /// never the elevating administrator's HKCU).
    /// </summary>
    User = 1,

    /// <summary>Every existing user profile plus the Default profile for future users.</summary>
    AllUsers = 2,
}
