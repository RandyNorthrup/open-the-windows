namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// How a profile is applied, independent of which entries it selects. These map
/// to apply-engine options and to the risk gate in <see cref="ProfileResolver"/>.
/// </summary>
/// <param name="RestorePoint">Create a System Restore point before applying.</param>
/// <param name="RestartExplorer">Restart Explorer for the interactive user when a selected entry requires it.</param>
/// <param name="AllowAdvanced">Permit <see cref="Model.RiskTier.Advanced"/> entries selected by a level dial. Explicit <c>include</c> ids are always allowed.</param>
/// <param name="AllowBreaking">Permit <see cref="Model.RiskTier.Breaking"/> entries selected by a level dial. Built-in profiles never set this.</param>
public sealed record ProfileOptions(
    bool RestorePoint,
    bool RestartExplorer,
    bool AllowAdvanced,
    bool AllowBreaking);
