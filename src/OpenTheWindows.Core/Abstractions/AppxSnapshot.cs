namespace OpenTheWindows.Core.Abstractions;

/// <summary>Observed installation state of an Appx/MSIX package.</summary>
/// <param name="InstalledForUser">Installed for the target user.</param>
/// <param name="InstalledForAnyUser">Installed for at least one user on the machine.</param>
/// <param name="Provisioned">Provisioned (staged for new users).</param>
/// <param name="Deprovisioned">Explicitly deprovisioned so new users do not receive it.</param>
public sealed record AppxSnapshot(bool InstalledForUser, bool InstalledForAnyUser, bool Provisioned, bool Deprovisioned);
