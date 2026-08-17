namespace OpenTheWindows.Core.Abstractions;

/// <summary>The outcome of running an allow-listed command through <see cref="ICommandRunner"/>.</summary>
/// <param name="ExitCode">Process exit code (0 is success for every allow-listed tool).</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
/// <param name="TimedOut">Whether the process was killed for exceeding its timeout.</param>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
