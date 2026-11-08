namespace OpenTheWindows.Cli.Commands;

/// <summary>The <c>eventlog status --json</c> result.</summary>
/// <param name="Source">The Event Log source name.</param>
/// <param name="Log">The log it writes to.</param>
/// <param name="Registered">Whether the source is registered.</param>
internal sealed record EventLogStatusJson(string Source, string Log, bool Registered);
