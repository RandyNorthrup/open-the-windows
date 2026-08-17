namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Runs an allow-listed executable with an explicit argument vector (no shell,
/// no argument-string interpolation, no user input). This is the only component
/// in the product permitted to start a process; the Windows implementation
/// carries the single banned-API suppression for <c>System.Diagnostics.Process</c>.
/// </summary>
public interface ICommandRunner
{
    /// <summary>
    /// Runs <paramref name="executable"/> with <paramref name="args"/>, killing it
    /// if it exceeds <paramref name="timeout"/> and reporting that in the result.
    /// </summary>
    CommandResult Run(string executable, IReadOnlyList<string> args, TimeSpan timeout);
}
