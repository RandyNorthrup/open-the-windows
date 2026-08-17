namespace OpenTheWindows.Core.Engine;

/// <summary>Options controlling one apply run.</summary>
/// <param name="WhatIf">Plan only; make no changes.</param>
/// <param name="CreateRestorePoint">Request a System Restore point before applying.</param>
/// <param name="BreakGlass">Apply even over MDM/Group-Policy-managed settings (audited).</param>
/// <param name="AllowAdvanced">Permit entries with <see cref="Model.RiskTier.Advanced"/> risk.</param>
/// <param name="AllowBreaking">Permit entries with <see cref="Model.RiskTier.Breaking"/> risk.</param>
/// <param name="ProfileName">The profile name recorded in the journal.</param>
/// <param name="RestartExplorer">Restart Explorer for entries that require it.</param>
public sealed record ApplyOptions(
    bool WhatIf,
    bool CreateRestorePoint,
    bool BreakGlass,
    bool AllowAdvanced,
    bool AllowBreaking,
    string ProfileName,
    bool RestartExplorer);
