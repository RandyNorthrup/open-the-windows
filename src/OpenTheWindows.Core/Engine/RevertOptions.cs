namespace OpenTheWindows.Core.Engine;

/// <summary>Options controlling one revert run.</summary>
/// <param name="WhatIf">Plan only; make no changes.</param>
/// <param name="RestartExplorer">Restart Explorer for entries that require it.</param>
/// <param name="Force">
/// Revert even when the run was already reverted, or when a later run changed the
/// same settings (whose changes the revert would then overwrite).
/// </param>
/// <param name="EntryIds">
/// Revert only these entries of the run (a per-entry revert); <see langword="null"/> reverts
/// the whole run.
/// </param>
public sealed record RevertOptions(bool WhatIf, bool RestartExplorer, bool Force = false, IReadOnlyCollection<string>? EntryIds = null);
