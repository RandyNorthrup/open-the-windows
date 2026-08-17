namespace OpenTheWindows.Core.Engine;

/// <summary>Options controlling one revert run.</summary>
/// <param name="WhatIf">Plan only; make no changes.</param>
/// <param name="RestartExplorer">Restart Explorer for entries that require it.</param>
public sealed record RevertOptions(bool WhatIf, bool RestartExplorer);
