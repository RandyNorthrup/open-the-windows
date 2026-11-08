namespace OpenTheWindows.Core.Engine;

/// <summary>Options controlling one revert run.</summary>
/// <param name="WhatIf">Plan only; make no changes.</param>
/// <param name="RestartExplorer">Restart Explorer for entries that require it.</param>
/// <param name="Force">
/// Revert even when the run was already reverted, or when a later run changed the
/// same settings (whose changes the revert would then overwrite).
/// </param>
public sealed record RevertOptions(bool WhatIf, bool RestartExplorer, bool Force = false);
