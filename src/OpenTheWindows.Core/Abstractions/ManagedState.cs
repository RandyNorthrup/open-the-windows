namespace OpenTheWindows.Core.Abstractions;

/// <summary>Whether a setting is managed by an organisation, and by which channel.</summary>
public enum ManagedState
{
    /// <summary>Not managed; the user/administrator owns the setting.</summary>
    NotManaged = 0,

    /// <summary>Managed by MDM (the Policy CSP / PolicyManager).</summary>
    Mdm = 1,

    /// <summary>Managed by Group Policy (Local GPO or domain GPO).</summary>
    GroupPolicy = 2,
}
